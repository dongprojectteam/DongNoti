using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using DongNoti;
using DongNoti.Models;

namespace DongNoti.Services
{
    public class AlarmService
    {
        private Timer? _checkTimer;
        private readonly List<Alarm> _alarms = new List<Alarm>();
        private readonly IStorageService _storageService;
        public event Action<Alarm>? AlarmTriggered;
        public event Action<IReadOnlyList<Alarm>>? AlarmsChanged;

        // 중복 트리거 방지 캐시 (AlarmId -> 마지막 트리거 분 단위 시간)
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _triggerCache = 
            new System.Collections.Concurrent.ConcurrentDictionary<string, DateTime>();
        
        // 변경 감지 플래그 (0: 변경 없음, 1: 변경됨)
        private long _isDirty = 0;

        /// <summary>
        /// 현재 메모리에 로드된 알람 목록을 반환합니다 (복사본)
        /// </summary>
        public List<Alarm> GetAlarms()
        {
            lock (_alarms)
            {
                return CloneAlarmsUnsafe();
            }
        }

        private static List<Alarm> CloneAlarms(IEnumerable<Alarm> alarms)
        {
            return alarms.Select(a => a.Clone()).ToList();
        }

        private List<Alarm> CloneAlarmsUnsafe()
        {
            return _alarms.Select(a => a.Clone()).ToList();
        }

        private void PersistAndNotify(List<Alarm> snapshot)
        {
            _storageService.SaveAlarms(CloneAlarms(snapshot));
            RaiseAlarmsChanged(snapshot);
        }

        private void RaiseAlarmsChanged(List<Alarm>? snapshot = null)
        {
            snapshot ??= GetAlarms();
            AlarmsChanged?.Invoke(CloneAlarms(snapshot));
        }

        public void AddAlarm(Alarm alarm)
        {
            if (alarm == null)
                throw new ArgumentNullException(nameof(alarm));

            List<Alarm> snapshot;
            lock (_alarms)
            {
                _alarms.Add(alarm.Clone());
                snapshot = CloneAlarmsUnsafe();
            }

            PersistAndNotify(snapshot);
        }

        public bool UpdateAlarm(Alarm alarm)
        {
            if (alarm == null)
                throw new ArgumentNullException(nameof(alarm));

            List<Alarm> snapshot;
            lock (_alarms)
            {
                var index = _alarms.FindIndex(a => a.Id == alarm.Id);
                if (index < 0)
                    return false;

                _alarms[index] = alarm.Clone();
                snapshot = CloneAlarmsUnsafe();
            }

            PersistAndNotify(snapshot);
            return true;
        }

        public bool DeleteAlarm(string alarmId)
        {
            if (string.IsNullOrWhiteSpace(alarmId))
                return false;

            List<Alarm> snapshot;
            lock (_alarms)
            {
                var removed = _alarms.RemoveAll(a => a.Id == alarmId);
                if (removed == 0)
                    return false;

                snapshot = CloneAlarmsUnsafe();
            }

            PersistAndNotify(snapshot);
            return true;
        }

        public int ReplaceAlarms(IEnumerable<Alarm> alarms)
        {
            if (alarms == null)
                throw new ArgumentNullException(nameof(alarms));

            List<Alarm> snapshot;
            lock (_alarms)
            {
                _alarms.Clear();
                _alarms.AddRange(CloneAlarms(alarms));
                snapshot = CloneAlarmsUnsafe();
            }

            PersistAndNotify(snapshot);
            return snapshot.Count;
        }

        public int RemoveAlarms(Func<Alarm, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            int removed;
            List<Alarm> snapshot;
            lock (_alarms)
            {
                removed = _alarms.RemoveAll(a => predicate(a.Clone()));
                if (removed == 0)
                    return 0;

                snapshot = CloneAlarmsUnsafe();
            }

            PersistAndNotify(snapshot);
            return removed;
        }

        public int UpdateAlarms(Func<Alarm, bool> predicate, Action<Alarm> update)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            int updated = 0;
            List<Alarm> snapshot;
            lock (_alarms)
            {
                foreach (var alarm in _alarms)
                {
                    if (!predicate(alarm.Clone()))
                        continue;

                    update(alarm);
                    updated++;
                }

                if (updated == 0)
                    return 0;

                snapshot = CloneAlarmsUnsafe();
            }

            PersistAndNotify(snapshot);
            return updated;
        }

        public bool SetAlarmEnabled(string alarmId, bool isEnabled)
        {
            return UpdateSingleAlarm(alarmId, alarm => alarm.IsEnabled = isEnabled);
        }

        public bool SetAutoRegisterAsDday(string alarmId, bool autoRegister)
        {
            return UpdateSingleAlarm(alarmId, alarm => alarm.AutoRegisterAsDday = autoRegister);
        }

        public bool SetLastTriggered(string alarmId, DateTime lastTriggered)
        {
            return UpdateSingleAlarm(alarmId, alarm => alarm.LastTriggered = lastTriggered);
        }

        public void SnoozeAlarm(string originalAlarmId, DateTime lastTriggered, Alarm snoozeAlarm)
        {
            if (snoozeAlarm == null)
                throw new ArgumentNullException(nameof(snoozeAlarm));

            List<Alarm> snapshot;
            lock (_alarms)
            {
                var originalAlarm = _alarms.FirstOrDefault(a => a.Id == originalAlarmId);
                if (originalAlarm != null)
                {
                    originalAlarm.LastTriggered = lastTriggered;
                }

                _alarms.Add(snoozeAlarm.Clone());
                snapshot = CloneAlarmsUnsafe();
            }

            PersistAndNotify(snapshot);
        }

        private bool UpdateSingleAlarm(string alarmId, Action<Alarm> update)
        {
            if (string.IsNullOrWhiteSpace(alarmId))
                return false;

            List<Alarm> snapshot;
            lock (_alarms)
            {
                var alarm = _alarms.FirstOrDefault(a => a.Id == alarmId);
                if (alarm == null)
                    return false;

                update(alarm);
                snapshot = CloneAlarmsUnsafe();
            }

            PersistAndNotify(snapshot);
            return true;
        }

        public AlarmService() : this(StorageService.Instance)
        {
        }

        public AlarmService(IStorageService storageService)
        {
            _storageService = storageService;
            LogService.LogInfo("AlarmService 초기화 시작");
            LoadAlarms();
            CleanupExpiredTemporaryAlarms();
            StartTimer();
            LogService.LogInfo($"AlarmService 초기화 완료. 알람 개수: {_alarms.Count}");
        }

        private void LoadAlarms()
        {
            lock (_alarms)
            {
                _alarms.Clear();
                var loadedAlarms = _storageService.LoadAlarms();
                _alarms.AddRange(CloneAlarms(loadedAlarms));
            }
        }

        private void MarkAsDirty()
        {
            System.Threading.Interlocked.Exchange(ref _isDirty, 1);
        }

        private void SaveIfDirty()
        {
            if (System.Threading.Interlocked.Exchange(ref _isDirty, 0) == 1)
            {
                List<Alarm> alarmsCopy;
                lock (_alarms)
                {
                    alarmsCopy = CloneAlarmsUnsafe();
                }
                
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        _storageService.SaveAlarms(alarmsCopy);
                        LogService.LogDebug("변경된 알람 목록 저장 완료");
                        RaiseAlarmsChanged(alarmsCopy);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("알람 목록 저장 중 오류", ex);
                        MarkAsDirty(); // 실패 시 다시 플래그 설정
                    }
                });
            }
        }

        /// <summary>
        /// 이미 지나간 임시 알람을 삭제합니다. (활성화 여부와 관계없이 삭제)
        /// </summary>
        private void CleanupExpiredTemporaryAlarms()
        {
            try
            {
                var now = DateTime.Now;
                List<Alarm> expiredTemporaryAlarms;
                
                lock (_alarms)
                {
                    expiredTemporaryAlarms = _alarms
                        .Where(a => a.IsTemporary && a.DateTime < now)
                        .ToList();

                    if (expiredTemporaryAlarms.Count > 0)
                    {
                        LogService.LogInfo($"지나간 임시 알람 {expiredTemporaryAlarms.Count}개 삭제 시작");
                        
                        foreach (var alarm in expiredTemporaryAlarms)
                        {
                            _alarms.Remove(alarm);
                            LogService.LogDebug($"임시 알람 삭제: '{alarm.Title}' ({alarm.DateTime:yyyy-MM-dd HH:mm})");
                        }
                        MarkAsDirty();
                    }
                }

                if (expiredTemporaryAlarms.Count > 0)
                {
                    SaveIfDirty();
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            if (System.Windows.Application.Current is App app && app.MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.RefreshAlarmsList();
                            }
                        }
                        catch (Exception refreshEx)
                        {
                            LogService.LogError("임시 알람 삭제 후 UI 업데이트 중 오류", refreshEx);
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("임시 알람 정리 중 오류", ex);
            }
        }

        public void RefreshAlarms()
        {
            LogService.LogDebug("알람 목록 새로고침");
            LoadAlarms();
            RaiseAlarmsChanged();
        }

        private void StartTimer()
        {
            _checkTimer = new Timer(10000);
            _checkTimer.Elapsed += CheckAlarms;
            _checkTimer.AutoReset = true;
            _checkTimer.Enabled = true;
            LogService.LogInfo("알람 체크 타이머 시작 (10초 간격)");
        }

        private void CheckAlarms(object? sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            var currentMinute = TimeHelper.ToMinutePrecision(now);
            
            // 캐시 정리 (오래된 캐시 삭제)
            var oldKeys = _triggerCache.Keys.Where(k => _triggerCache[k] < currentMinute.AddMinutes(-1)).ToList();
            foreach (var key in oldKeys) _triggerCache.TryRemove(key, out _);

            CleanupExpiredTemporaryAlarms();
            SaveIfDirty(); // 다른 작업에 의해 Dirty가 된 경우 저장

            List<Alarm> enabledAlarms;
            lock (_alarms)
            {
                enabledAlarms = _alarms.Where(a => a.IsEnabled).ToList();
            }

            foreach (var alarm in enabledAlarms)
            {
                try
                {
                    var nextAlarmTime = alarm.GetNextAlarmTime();
                    if (nextAlarmTime.HasValue)
                    {
                        var alarmMinute = TimeHelper.ToMinutePrecision(nextAlarmTime.Value);
                        
                        if (alarmMinute == currentMinute)
                        {
                            // 중복 트리거 방지: 메모리 캐시 확인
                            if (!_triggerCache.TryGetValue(alarm.Id, out var lastTriggered) || lastTriggered < currentMinute)
                            {
                                // 캐시 업데이트
                                _triggerCache[alarm.Id] = currentMinute;
                                
                                if (FocusModeService.Instance.IsFocusModeActive)
                                {
                                    FocusModeService.Instance.RecordMissedAlarm(alarm);
                                    RecordAlarmHistory(alarm, wasMissed: true);
                                    alarm.LastTriggered = currentMinute;
                                    AutoRegisterNextAlarmAsDday(alarm);
                                    MarkAsDirty();
                                }
                                else
                                {
                                    alarm.LastTriggered = currentMinute;
                                    AutoRegisterNextAlarmAsDday(alarm);
                                    LogService.LogInfo($"알람 트리거: '{alarm.Title}' at {currentMinute:yyyy-MM-dd HH:mm}");
                                    
                                    RecordAlarmHistory(alarm, wasMissed: false);
                                    AlarmTriggered?.Invoke(alarm);

                                    if (alarm.IsTemporary)
                                    {
                                        lock (_alarms) { _alarms.Remove(alarm); }
                                        MarkAsDirty();
                                        
                                        // UI 갱신은 App 클래스에서 처리하도록 위임하거나 여기서 예약
                                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            if (System.Windows.Application.Current is App app)
                                            {
                                                app.RefreshAlarms(refreshMainWindow: true);
                                            }
                                        }), System.Windows.Threading.DispatcherPriority.Background);
                                    }
                                    else
                                    {
                                        MarkAsDirty();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError($"알람 '{alarm.Title}' 체크 중 오류 발생", ex);
                }
            }
            
            SaveIfDirty();
        }

        public void Stop()
        {
            LogService.LogInfo("AlarmService 종료");
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
        }

        /// <summary>
        /// 지정한 알람에 대해 "다음" Dday를 즉시 생성합니다. (리스트에서 Dday 등록 켤 때, 새 알람/복제 시 사용)
        /// 이미 해당 Dday가 있으면 무시됩니다.
        /// </summary>
        public void CreateNextDdayForAlarm(string alarmId)
        {
            var alarm = _alarms.FirstOrDefault(a => a.Id == alarmId);
            if (alarm == null)
                return;
            AutoRegisterNextAlarmAsDday(alarm);
        }

        /// <summary>
        /// 알람이 울릴 때 다음 알람 시간을 Dday로 자동 등록합니다.
        /// </summary>
        private void AutoRegisterNextAlarmAsDday(Alarm alarm)
        {
            try
            {
                if (!alarm.AutoRegisterAsDday || alarm.AlarmType != AlarmType.Alarm)
                    return;

                DateTime? nextAlarmDate = null;

                if (alarm.RepeatType == RepeatType.None)
                {
                    nextAlarmDate = alarm.DateTime.Date;
                }
                else if (alarm.RepeatType == RepeatType.Daily)
                {
                    nextAlarmDate = DateTime.Now.Date.AddDays(1);
                }
                else if (alarm.RepeatType == RepeatType.Weekly)
                {
                    var selectedDays = alarm.SelectedDaysOfWeek?.Any() == true
                        ? alarm.SelectedDaysOfWeek
                        : new List<DayOfWeek> { alarm.DateTime.DayOfWeek };

                    var today = DateTime.Now.Date;
                    var currentDayOfWeek = today.DayOfWeek;

                    foreach (var dayOfWeek in selectedDays)
                    {
                        int daysUntilTarget = ((int)dayOfWeek - (int)currentDayOfWeek + 7) % 7;
                        if (daysUntilTarget == 0) daysUntilTarget = 7;

                        var targetDate = today.AddDays(daysUntilTarget);
                        CreateDdayIfNotExists(alarm, targetDate);
                    }
                    return;
                }
                else if (alarm.RepeatType == RepeatType.Monthly)
                {
                    var nextMonth = DateTime.Now.AddMonths(1);
                    var validDay = Math.Min(alarm.DateTime.Day, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    nextAlarmDate = new DateTime(nextMonth.Year, nextMonth.Month, validDay);
                }
                else if (alarm.RepeatType == RepeatType.Yearly)
                {
                    var nextYear = DateTime.Now.Year + 1;
                    var validDay = Math.Min(alarm.DateTime.Day, DateTime.DaysInMonth(nextYear, alarm.DateTime.Month));
                    nextAlarmDate = new DateTime(nextYear, alarm.DateTime.Month, validDay);
                }

                if (nextAlarmDate.HasValue)
                {
                    CreateDdayIfNotExists(alarm, nextAlarmDate.Value);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"Dday 자동 등록 중 오류 ('{alarm.Title}')", ex);
            }
        }

        /// <summary>
        /// 같은 날짜/제목의 Dday가 없으면 생성합니다.
        /// </summary>
        private void CreateDdayIfNotExists(Alarm sourceAlarm, DateTime targetDate)
        {
            var ddayTitle = $"[자동] {sourceAlarm.Title}";

            var exists = _alarms.Any(a =>
                a.AlarmType == AlarmType.Dday &&
                a.Title == ddayTitle &&
                a.TargetDate.HasValue &&
                a.TargetDate.Value.Date == targetDate.Date);

            if (exists)
            {
                LogService.LogDebug($"Dday 이미 존재함: '{ddayTitle}' ({targetDate:yyyy-MM-dd})");
                return;
            }

            var newDday = new Alarm
            {
                Id = Guid.NewGuid().ToString(),
                Title = ddayTitle,
                AlarmType = AlarmType.Dday,
                TargetDate = targetDate,
                IsEnabled = true,
                Category = sourceAlarm.Category,
                Priority = sourceAlarm.Priority,
                RepeatType = RepeatType.None,
                DateTime = DateTime.Now,
                Memo = $"{sourceAlarm.Title} 알람에서 자동 생성됨",
                SoundFilePath = null,
                SelectedDaysOfWeek = new List<DayOfWeek>(),
                AutoDismissMinutes = 1,
                AutoRegisterAsDday = false
            };

            _alarms.Add(newDday);
            LogService.LogInfo($"Dday 자동 등록: '{ddayTitle}' ({targetDate:yyyy-MM-dd})");

            PersistAndNotify(CloneAlarmsUnsafe());

            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (System.Windows.Application.Current is App app)
                    {
                        app.RefreshAlarms(refreshMainWindow: true);
                        var ddayWindow = app.GetDdayWindow();
                        if (ddayWindow != null && ddayWindow.IsVisible)
                        {
                            ddayWindow.RefreshDdayList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError("Dday 자동 등록 후 UI 업데이트 중 오류", ex);
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 알람 트리거 히스토리를 기록합니다.
        /// </summary>
        public void RecordAlarmHistory(Alarm alarm, bool wasMissed)
        {
            try
            {
                var settings = _storageService.LoadSettings();
                if (settings.AlarmHistory == null)
                {
                    settings.AlarmHistory = new List<AlarmHistory>();
                }

                var now = DateTime.Now;
                var currentMinute = TimeHelper.ToMinutePrecision(now);
                
                var existingHistory = settings.AlarmHistory
                    .Where(h => h.AlarmId == alarm.Id)
                    .OrderByDescending(h => h.TriggeredAt)
                    .FirstOrDefault();
                
                if (existingHistory != null)
                {
                    var historyMinute = new DateTime(
                        existingHistory.TriggeredAt.Year,
                        existingHistory.TriggeredAt.Month,
                        existingHistory.TriggeredAt.Day,
                        existingHistory.TriggeredAt.Hour,
                        existingHistory.TriggeredAt.Minute,
                        0);
                    
                    if (historyMinute == currentMinute)
                    {
                        existingHistory.WasMissed = wasMissed;
                        _storageService.SaveSettings(settings);
                        LogService.LogDebug($"알람 히스토리 업데이트: '{alarm.Title}' WasMissed={wasMissed}");
                        return;
                    }
                }

                var history = new AlarmHistory
                {
                    AlarmId = alarm.Id,
                    AlarmTitle = alarm.Title,
                    TriggeredAt = now,
                    WasMissed = wasMissed
                };

                settings.AlarmHistory.Add(history);
                if (settings.AlarmHistory.Count > 1000)
                {
                    settings.AlarmHistory = settings.AlarmHistory
                        .OrderByDescending(h => h.TriggeredAt)
                        .Take(1000)
                        .ToList();
                }

                _storageService.SaveSettings(settings);
                LogService.LogDebug($"알람 히스토리 추가: '{alarm.Title}' WasMissed={wasMissed}");
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 히스토리 기록 중 오류", ex);
            }
        }
    }
}

