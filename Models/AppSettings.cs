using System;
using System.Collections.Generic;

namespace DongNoti.Models
{
    public class AppSettings
    {
        public bool RunOnStartup { get; set; } = true;
        public bool HideToTrayOnStartup { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool EnableLogging { get; set; } = false; // 기본값: 켜짐
        public bool ShowUILog { get; set; } = false; // UI 로그 창 표시 (기본값: 꺼짐)

        // 집중모드 관련 설정
        public bool FocusModeActive { get; set; } = false;
        public DateTime? FocusModeEndTime { get; set; }
        public List<FocusModePreset> FocusModePresets { get; set; } = new List<FocusModePreset>();
        public string DefaultFocusModePresetId { get; set; } = "30m";
        public List<MissedAlarm> CurrentMissedAlarms { get; set; } = new List<MissedAlarm>();
        
        // 통계 데이터
        public List<AlarmHistory> AlarmHistory { get; set; } = new List<AlarmHistory>();
        
        // 알람 카테고리 목록
        public List<string> AlarmCategories { get; set; } = GetDefaultAlarmCategories();

        // 카테고리별 색상 (D-Day 창·메인 창에서 사용, 키: 카테고리명, 값: hex 색상 예 "#E91E63")
        public Dictionary<string, string> CategoryColors { get; set; } = new Dictionary<string, string> { ["기념일"] = "#E91E63" };
        
        // Dday 창 표시 상태
        public bool DdayWindowVisible { get; set; } = false;

        /// <summary>
        /// 기본 프리셋 목록을 반환합니다 (30분부터 3시간까지 30분 간격)
        /// </summary>
        public static List<FocusModePreset> GetDefaultPresets()
        {
            return new List<FocusModePreset>
            {
                new FocusModePreset("30m", "30분", 30),
                new FocusModePreset("1h", "1시간", 60),
                new FocusModePreset("1h30m", "1시간 30분", 90),
                new FocusModePreset("2h", "2시간", 120),
                new FocusModePreset("2h30m", "2시간 30분", 150),
                new FocusModePreset("3h", "3시간", 180)
            };
        }

        public static List<string> GetDefaultAlarmCategories()
        {
            return new List<string> { "기본", "업무", "개인", "약속", "기념일" };
        }
    }
}

