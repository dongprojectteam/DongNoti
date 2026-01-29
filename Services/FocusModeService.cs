using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using DongNoti.Models;

namespace DongNoti.Services
{
    public class FocusModeService
    {
        private static FocusModeService? _instance;
        private Timer? _updateTimer;
        private AppSettings? _settings;

        public static FocusModeService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FocusModeService();
                }
                return _instance;
            }
        }

        // 집중모드 상태 변경 이벤트
        public event Action<bool>? FocusModeChanged;
        public event Action? FocusModeEnded;

        private FocusModeService()
        {
            _settings = StorageService.LoadSettings();
            
            // 기본 프리셋이 없으면 초기화
            if (_settings.FocusModePresets == null || _settings.FocusModePresets.Count == 0)
            {
                _settings.FocusModePresets = AppSettings.GetDefaultPresets();
                StorageService.SaveSettings(_settings);
            }

            // 앱 시작 시 집중모드가 활성화되어 있으면 복원
            if (_settings.FocusModeActive && _settings.FocusModeEndTime.HasValue)
            {
                if (_settings.FocusModeEndTime.Value > DateTime.Now)
                {
                    // 아직 종료 시간이 안 됨 - 타이머 시작
                    StartUpdateTimer();
                    LogService.LogInfo($"집중모드 복원: 종료 시간 {_settings.FocusModeEndTime.Value:yyyy-MM-dd HH:mm}");
                }
                else
                {
                    // 종료 시간이 지남 - 집중모드 종료
                    LogService.LogInfo("집중모드 종료 시간이 지나 자동 종료");
                    StopFocusMode();
                }
            }
        }

        public bool IsFocusModeActive
        {
            get
            {
                if (_settings == null)
                {
                    _settings = StorageService.LoadSettings();
                }
                return _settings.FocusModeActive && 
                       _settings.FocusModeEndTime.HasValue && 
                       _settings.FocusModeEndTime.Value > DateTime.Now;
            }
        }

        public DateTime? EndTime => _settings?.FocusModeEndTime;

        /// <summary>
        /// 집중모드를 시작합니다.
        /// </summary>
        public void StartFocusMode(int minutes)
        {
            try
            {
                _settings = StorageService.LoadSettings();
                _settings.FocusModeActive = true;
                _settings.FocusModeEndTime = DateTime.Now.AddMinutes(minutes);
                _settings.CurrentMissedAlarms = new List<MissedAlarm>();
                
                StorageService.SaveSettings(_settings);
                
                StartUpdateTimer();
                
                LogService.LogInfo($"집중모드 시작: {minutes}분 (종료 시간: {_settings.FocusModeEndTime.Value:HH:mm})");
                FocusModeChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                LogService.LogError("집중모드 시작 중 오류", ex);
            }
        }

        /// <summary>
        /// 집중모드를 종료합니다.
        /// </summary>
        public void StopFocusMode()
        {
            try
            {
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                _updateTimer = null;

                _settings = StorageService.LoadSettings();
                var hadMissedAlarms = _settings.CurrentMissedAlarms?.Count > 0;
                
                _settings.FocusModeActive = false;
                _settings.FocusModeEndTime = null;
                // CurrentMissedAlarms는 요약창 표시를 위해 유지
                
                StorageService.SaveSettings(_settings);
                
                LogService.LogInfo($"집중모드 종료 (놓친 알람: {_settings.CurrentMissedAlarms?.Count ?? 0}개)");
                FocusModeChanged?.Invoke(false);
                
                if (hadMissedAlarms)
                {
                    FocusModeEnded?.Invoke();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("집중모드 종료 중 오류", ex);
            }
        }

        /// <summary>
        /// 남은 시간을 반환합니다.
        /// </summary>
        public TimeSpan GetRemainingTime()
        {
            if (_settings?.FocusModeEndTime.HasValue == true)
            {
                var remaining = _settings.FocusModeEndTime.Value - DateTime.Now;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// 놓친 알람을 기록합니다.
        /// </summary>
        public void RecordMissedAlarm(Alarm alarm)
        {
            try
            {
                _settings = StorageService.LoadSettings();
                
                if (_settings.CurrentMissedAlarms == null)
                {
                    _settings.CurrentMissedAlarms = new List<MissedAlarm>();
                }

                // 중복 체크 (같은 알람 ID가 이미 있으면 기록하지 않음)
                if (_settings.CurrentMissedAlarms.Any(m => m.AlarmId == alarm.Id))
                {
                    LogService.LogDebug($"놓친 알람 중복 기록 방지: '{alarm.Title}'");
                    return;
                }

                var nextTime = alarm.GetNextAlarmTime();
                var missedAlarm = new MissedAlarm(
                    alarm.Id,
                    alarm.Title,
                    nextTime ?? alarm.DateTime,
                    alarm.RepeatTypeString
                );

                _settings.CurrentMissedAlarms.Add(missedAlarm);
                StorageService.SaveSettings(_settings);
                
                LogService.LogInfo($"놓친 알람 기록: '{alarm.Title}' ({missedAlarm.ScheduledTime:HH:mm})");
            }
            catch (Exception ex)
            {
                LogService.LogError("놓친 알람 기록 중 오류", ex);
            }
        }

        /// <summary>
        /// 놓친 알람 목록을 반환합니다.
        /// </summary>
        public List<MissedAlarm> GetMissedAlarms()
        {
            _settings = StorageService.LoadSettings();
            return _settings.CurrentMissedAlarms ?? new List<MissedAlarm>();
        }

        /// <summary>
        /// 놓친 알람 목록을 초기화합니다.
        /// </summary>
        public void ClearMissedAlarms()
        {
            try
            {
                _settings = StorageService.LoadSettings();
                _settings.CurrentMissedAlarms = new List<MissedAlarm>();
                StorageService.SaveSettings(_settings);
                LogService.LogDebug("놓친 알람 목록 초기화");
            }
            catch (Exception ex)
            {
                LogService.LogError("놓친 알람 목록 초기화 중 오류", ex);
            }
        }

        /// <summary>
        /// 프리셋 목록을 반환합니다.
        /// </summary>
        public List<FocusModePreset> GetPresets()
        {
            _settings = StorageService.LoadSettings();
            if (_settings.FocusModePresets == null || _settings.FocusModePresets.Count == 0)
            {
                return AppSettings.GetDefaultPresets();
            }
            return _settings.FocusModePresets;
        }

        /// <summary>
        /// 기본 프리셋을 반환합니다.
        /// </summary>
        public FocusModePreset? GetDefaultPreset()
        {
            _settings = StorageService.LoadSettings();
            var presets = GetPresets();
            
            // DefaultFocusModePresetId로 찾기
            var defaultPreset = presets.FirstOrDefault(p => p.Id == _settings.DefaultFocusModePresetId);
            
            // 없으면 첫 번째 프리셋 반환
            return defaultPreset ?? presets.FirstOrDefault();
        }

        private void StartUpdateTimer()
        {
            // 기존 타이머가 있으면 정리
            _updateTimer?.Stop();
            _updateTimer?.Dispose();

            // 1분마다 체크하는 타이머 시작
            _updateTimer = new Timer(60000); // 60초
            _updateTimer.Elapsed += OnTimerElapsed;
            _updateTimer.AutoReset = true;
            _updateTimer.Start();
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                _settings = StorageService.LoadSettings();
                
                // 종료 시간 체크
                if (_settings.FocusModeEndTime.HasValue && DateTime.Now >= _settings.FocusModeEndTime.Value)
                {
                    LogService.LogInfo("집중모드 자동 종료 (시간 만료)");
                    StopFocusMode();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("집중모드 타이머 처리 중 오류", ex);
            }
        }

        public void Shutdown()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
        }
    }
}
