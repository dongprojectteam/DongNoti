using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
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
            CleanupExpiredTemporaryAlarms(); // 지나간 임시 알람 정리
            StartTimer();
            LogService.LogInfo($"AlarmService 초기화 완료. 알람 개수: {_alarms.Count}");
        }

        private void LoadAlarms()
        {
            _alarms.Clear();
            var loadedAlarms = StorageService.LoadAlarms();
            _alarms.AddRange(loadedAlarms);
            // StorageService.LoadAlarms()에서 이미 로그를 출력하므로 중복 로그 제거
        }

        /// <summary>
        /// 이미 지나간 임시 알람을 삭제합니다. (활성화 여부와 관계없이 삭제)
        /// </summary>
        private void CleanupExpiredTemporaryAlarms()
        {
            try
            {
                var now = DateTime.Now;
                // 활성화 여부와 관계없이 지나간 임시 알람 모두 삭제
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

                    // 변경사항을 JSON에 저장
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        StorageService.SaveAlarms(_alarms.ToList());
                        LogService.LogInfo("지나간 임시 알람 삭제 완료 및 저장");
                        
                        // MainWindow UI 업데이트 (ItemsSource를 null로 설정하지 않음)
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
            // 동기적으로 실행하여 즉시 반영
            LogService.LogDebug("알람 목록 새로고침");
            LoadAlarms();
        }

        private void StartTimer()
        {
            _checkTimer = new Timer(10000); // 10초마다 체크 (더 정확한 타이밍)
            _checkTimer.Elapsed += CheckAlarms;
            _checkTimer.AutoReset = true;
            _checkTimer.Enabled = true;
            LogService.LogInfo("알람 체크 타이머 시작 (10초 간격)");
        }

        private void CheckAlarms(object? sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            // 지나간 임시 알람 정리 (활성화 여부와 관계없이)
            CleanupExpiredTemporaryAlarms();

            // 집중모드 체크
            if (FocusModeService.Instance.IsFocusModeActive)
            {
                // 울려야 할 알람을 기록
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

                            // 정확히 같은 분인지 확인
                            if (alarmMinute.Year == currentMinute.Year &&
                                alarmMinute.Month == currentMinute.Month &&
                                alarmMinute.Day == currentMinute.Day &&
                                alarmMinute.Hour == currentMinute.Hour &&
                                alarmMinute.Minute == currentMinute.Minute)
                            {
                                // 중복 트리거 방지
                                if (alarm.LastTriggered == null || 
                                    alarm.LastTriggered.Value < currentMinute)
                                {
                                    // 놓친 알람으로 기록
                                    FocusModeService.Instance.RecordMissedAlarm(alarm);
                                    
                                    // 히스토리 기록 (놓친 알람) - 집중 모드에서는 즉시 기록
                                    RecordAlarmHistory(alarm, wasMissed: true);
                                    
                                    // LastTriggered 업데이트 (다음 체크 시 중복 기록 방지)
                                    alarm.LastTriggered = currentMinute;
                                    
                                    // 임시 알람이 아닌 경우에만 저장
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
                return; // 알람 트리거 건너뛰기
            }

            var enabledAlarms = _alarms.Where(a => a.IsEnabled).ToList();
            // LogService.LogDebug 제거 - 10초마다 호출되면 너무 많아서 디스크 I/O 발생

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

                        // LogService.LogDebug 제거 - 각 알람마다 호출되면 너무 많음

                        // 정확히 같은 분인지 확인 (년, 월, 일, 시, 분이 모두 일치)
                        if (alarmMinute.Year == currentMinute.Year &&
                            alarmMinute.Month == currentMinute.Month &&
                            alarmMinute.Day == currentMinute.Day &&
                            alarmMinute.Hour == currentMinute.Hour &&
                            alarmMinute.Minute == currentMinute.Minute)
                        {
                            // 중복 트리거 방지: 마지막 트리거 시간이 현재 분과 다를 때만
                            if (alarm.LastTriggered == null || 
                                alarm.LastTriggered.Value < currentMinute)
                            {
                                alarm.LastTriggered = currentMinute;
                                LogService.LogInfo($"알람 트리거: '{alarm.Title}' at {currentMinute:yyyy-MM-dd HH:mm}");
                                
                                // 일시적 알람이면 삭제 예약
                                bool shouldDelete = alarm.IsTemporary;
                                
                                // LastTriggered를 JSON에 저장 (일시적 알람이 아닌 경우)
                                // 일시적 알람은 나중에 삭제할 것이므로 저장 생략
                                if (!shouldDelete)
                                {
                                    try
                                    {
                                        // 백그라운드 스레드에서 저장하여 UI 블로킹 방지
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
                                
                                // 히스토리 초기 기록 (기본값: 놓친 알람으로 가정, 사용자 동작에 따라 업데이트됨)
                                RecordAlarmHistory(alarm, wasMissed: true);
                                
                                AlarmTriggered?.Invoke(alarm);
                                
                                // 일시적 알람 삭제 (백그라운드 스레드에서 안전하게)
                                if (shouldDelete)
                                {
                                    System.Threading.Tasks.Task.Run(() =>
                                    {
                                        try
                                        {
                                            // 잠깐 대기 (AlarmPopup이 닫힐 시간 확보)
                                            System.Threading.Thread.Sleep(500);
                                            
                                            LogService.LogInfo($"일시적 알람 삭제 시작: '{alarm.Title}'");
                                            _alarms.Remove(alarm);
                                            StorageService.SaveAlarms(_alarms.ToList());
                                            LogService.LogDebug("일시적 알람 삭제 및 저장 완료");
                                            
                                            // MainWindow UI만 업데이트 (ItemsSource를 null로 설정하지 않음)
                                            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                            {
                                                try
                                                {
                                                    if (System.Windows.Application.Current is App app)
                                                    {
                                                        // AlarmService 새로고침 (백그라운드)
                                                        app.RefreshAlarms(refreshMainWindow: false);
                                                        
                                                        // MainWindow의 알람 목록만 직접 업데이트
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
                            // else 블록의 LogService.LogDebug 제거 - 중복 트리거는 정상 동작이므로 로그 불필요
                        }
                    }
                    // else 블록의 LogService.LogDebug 제거 - 다음 알람 시간이 없는 것도 정상 동작
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

                // 같은 알람의 최근 히스토리 확인 (같은 분에 트리거된 경우 업데이트)
                var now = DateTime.Now;
                var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                
                // 같은 알람의 같은 분에 기록된 히스토리가 있는지 확인
                var existingHistory = settings.AlarmHistory
                    .Where(h => h.AlarmId == alarm.Id)
                    .OrderByDescending(h => h.TriggeredAt)
                    .FirstOrDefault();
                
                // 같은 분에 트리거된 경우 기존 기록 업데이트 (사용자 동작에 따라 덮어쓰기)
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
                        // 기존 기록 업데이트
                        existingHistory.WasMissed = wasMissed;
                        StorageService.SaveSettings(settings);
                        LogService.LogDebug($"알람 히스토리 업데이트: '{alarm.Title}' WasMissed={wasMissed}");
                        return;
                    }
                }

                // 새로운 히스토리 추가
                var history = new AlarmHistory
                {
                    AlarmId = alarm.Id,
                    AlarmTitle = alarm.Title,
                    TriggeredAt = now,
                    WasMissed = wasMissed
                };

                settings.AlarmHistory.Add(history);
                
                // 최근 1000개만 유지 (메모리 절약)
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

