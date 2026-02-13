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
        public event Action<Alarm>? AlarmTriggered;

        /// <summary>
        /// 현재 메모리에 로드된 알람 목록을 반환합니다 (복사본)
        /// </summary>
        public List<Alarm> GetAlarms()
        {
            return new List<Alarm>(_alarms);
        }

        public AlarmService()
        {
            LogService.LogInfo("AlarmService 초기화 시작");
            LoadAlarms();
            CleanupExpiredTemporaryAlarms();
            StartTimer();
            LogService.LogInfo($"AlarmService 초기화 완료. 알람 개수: {_alarms.Count}");
        }

        private void LoadAlarms()
        {
            _alarms.Clear();
            var loadedAlarms = StorageService.LoadAlarms();
            _alarms.AddRange(loadedAlarms);
        }

        /// <summary>
        /// 이미 지나간 임시 알람을 삭제합니다. (활성화 여부와 관계없이 삭제)
        /// </summary>
        private void CleanupExpiredTemporaryAlarms()
        {
            try
            {
                var now = DateTime.Now;
                var expiredTemporaryAlarms = _alarms
                    .Where(a => a.IsTemporary && a.DateTime < now)
                    .ToList();

                if (expiredTemporaryAlarms.Count > 0)
                {
                    LogService.LogInfo($"지나간 임시 알람 {expiredTemporaryAlarms.Count}개 삭제 시작");
                    
                    foreach (var alarm in expiredTemporaryAlarms)
                    {
                        _alarms.Remove(alarm);
                        LogService.LogDebug($"임시 알람 삭제: '{alarm.Title}' ({alarm.DateTime:yyyy-MM-dd HH:mm}, 활성화: {alarm.IsEnabled})");
                    }
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        StorageService.SaveAlarms(_alarms.ToList());
                        LogService.LogInfo("지나간 임시 알람 삭제 완료 및 저장");
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
                    });
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
            CleanupExpiredTemporaryAlarms();
            if (FocusModeService.Instance.IsFocusModeActive)
            {
                var enabledAlarmsForFocus = _alarms.Where(a => a.IsEnabled).ToList();
                foreach (var alarm in enabledAlarmsForFocus)
                {
                    try
                    {
                        var nextAlarmTime = alarm.GetNextAlarmTime();
                        if (nextAlarmTime.HasValue)
                        {
                            var alarmMinute = new DateTime(
                                nextAlarmTime.Value.Year,
                                nextAlarmTime.Value.Month,
                                nextAlarmTime.Value.Day,
                                nextAlarmTime.Value.Hour,
                                nextAlarmTime.Value.Minute,
                                0);
                            if (alarmMinute.Year == currentMinute.Year &&
                                alarmMinute.Month == currentMinute.Month &&
                                alarmMinute.Day == currentMinute.Day &&
                                alarmMinute.Hour == currentMinute.Hour &&
                                alarmMinute.Minute == currentMinute.Minute)
                            {
                                if (alarm.LastTriggered == null || 
                                    alarm.LastTriggered.Value < currentMinute)
                                {
                                    FocusModeService.Instance.RecordMissedAlarm(alarm);
                                    RecordAlarmHistory(alarm, wasMissed: true);
                                    alarm.LastTriggered = currentMinute;
                                    if (!alarm.IsTemporary)
                                    {
                                        System.Threading.Tasks.Task.Run(() =>
                                        {
                                            StorageService.SaveAlarms(_alarms.ToList());
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError($"집중모드 중 알람 '{alarm.Title}' 체크 중 오류", ex);
                    }
                }
                return;
            }
            var enabledAlarms = _alarms.Where(a => a.IsEnabled).ToList();
            foreach (var alarm in enabledAlarms)
            {
                try
                {
                    var nextAlarmTime = alarm.GetNextAlarmTime();
                    if (nextAlarmTime.HasValue)
                    {
                        var alarmMinute = new DateTime(
                            nextAlarmTime.Value.Year,
                            nextAlarmTime.Value.Month,
                            nextAlarmTime.Value.Day,
                            nextAlarmTime.Value.Hour,
                            nextAlarmTime.Value.Minute,
                                0);
                        if (alarmMinute.Year == currentMinute.Year &&
                            alarmMinute.Month == currentMinute.Month &&
                            alarmMinute.Day == currentMinute.Day &&
                            alarmMinute.Hour == currentMinute.Hour &&
                            alarmMinute.Minute == currentMinute.Minute)
                        {
                            if (alarm.LastTriggered == null || 
                                alarm.LastTriggered.Value < currentMinute)
                            {
                                alarm.LastTriggered = currentMinute;
                                LogService.LogInfo($"알람 트리거: '{alarm.Title}' at {currentMinute:yyyy-MM-dd HH:mm}");
                                bool shouldDelete = alarm.IsTemporary;
                                if (!shouldDelete)
                                {
                                    try
                                    {
                                        System.Threading.Tasks.Task.Run(() =>
                                        {
                                            StorageService.SaveAlarms(_alarms.ToList());
                                            LogService.LogDebug("알람 트리거 시간 저장 완료");
                                        });
                                    }
                                    catch (Exception saveEx)
                                    {
                                        LogService.LogError("알람 트리거 시간 저장 중 오류", saveEx);
                                    }
                                }
                                RecordAlarmHistory(alarm, wasMissed: true);
                                AlarmTriggered?.Invoke(alarm);
                                if (shouldDelete)
                                {
                                    System.Threading.Tasks.Task.Run(() =>
                                    {
                                        try
                                        {
                                            System.Threading.Thread.Sleep(500);
                                            
                                            LogService.LogInfo($"일시적 알람 삭제 시작: '{alarm.Title}'");
                                            _alarms.Remove(alarm);
                                            StorageService.SaveAlarms(_alarms.ToList());
                                            LogService.LogDebug("일시적 알람 삭제 및 저장 완료");
                                            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                            {
                                                try
                                                {
                                                    if (System.Windows.Application.Current is App app)
                                                    {
                                                        app.RefreshAlarms(refreshMainWindow: false);
                                                        app.Dispatcher.BeginInvoke(() =>
                                                        {
                                                            try
                                                            {
                                                                if (app.MainWindow is MainWindow mainWindow)
                                                                {
                                                                    mainWindow.RefreshAlarmsList();
                                                                    LogService.LogInfo("일시적 알람 삭제 후 UI 새로고침 완료");
                                                                }
                                                            }
                                                            catch (Exception refreshEx)
                                                            {
                                                                LogService.LogError("MainWindow 알람 목록 새로고침 중 오류", refreshEx);
                                                            }
                                                        }, System.Windows.Threading.DispatcherPriority.Background);
                                                    }
                                                }
                                                catch (Exception refreshEx)
                                                {
                                                    LogService.LogError("UI 새로고침 중 오류", refreshEx);
                                                }
                                            }), System.Windows.Threading.DispatcherPriority.Background);
                                        }
                                        catch (Exception deleteEx)
                                        {
                                            LogService.LogError("일시적 알람 삭제 중 오류", deleteEx);
                                        }
                                    });
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
        }

        public void Stop()
        {
            LogService.LogInfo("AlarmService 종료");
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
        }

        /// <summary>
        /// 알람 트리거 히스토리를 기록합니다.
        /// </summary>
        public void RecordAlarmHistory(Alarm alarm, bool wasMissed)
        {
            try
            {
                var settings = StorageService.LoadSettings();
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
                        StorageService.SaveSettings(settings);
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

                StorageService.SaveSettings(settings);
                LogService.LogDebug($"알람 히스토리 추가: '{alarm.Title}' WasMissed={wasMissed}");
            }
            catch (Exception ex)
            {
                LogService.LogError("알람 히스토리 기록 중 오류", ex);
            }
        }
    }
}

