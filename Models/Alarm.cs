using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DongNoti.Models
{
    public enum RepeatType
    {
        None,      // 반복 없음
        Daily,     // 매일
        Weekly,    // 매주
        Monthly    // 매월
    }

    public enum AlarmType
    {
        Alarm,     // 일반 알람
        Dday       // Dday (카운트다운)
    }

    public class Alarm
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "알람";
        public DateTime DateTime { get; set; } = DateTime.Now.AddHours(1);
        public RepeatType RepeatType { get; set; } = RepeatType.None;
        public bool IsEnabled { get; set; } = true;
        public string? SoundFilePath { get; set; } // null이면 기본 사운드 사용
        public DateTime? LastTriggered { get; set; } // 마지막으로 알람이 울린 시간
        public List<DayOfWeek> SelectedDaysOfWeek { get; set; } = new List<DayOfWeek>(); // 매주 반복 시 선택된 요일
        public bool IsTemporary { get; set; } = false; // 일시적인 알람 (울린 후 자동 삭제)
        public int AutoDismissMinutes { get; set; } = 1; // 알람 팝업 자동 종료 시간 (분)
        public string? Category { get; set; } // 카테고리 (nullable, 기본값: null → "기본"으로 표시)
        public Priority Priority { get; set; } = Priority.Normal; // 우선순위 (기본값: Normal)
        public AlarmType AlarmType { get; set; } = AlarmType.Alarm; // 알람 타입 (기본값: Alarm, 기존 알람 호환성)
        public DateTime? TargetDate { get; set; } // Dday 타입일 때 목표 날짜
        public string? Memo { get; set; } // Dday 타입일 때 메모

        [JsonIgnore]
        public string CategoryDisplay => Category ?? "기본";

        [JsonIgnore]
        public int PrioritySortKey => (int)Priority;

        /// <summary>
        /// Dday 타입일 때 남은 일수 계산 (음수는 지난 날짜)
        /// </summary>
        [JsonIgnore]
        public int? DaysRemaining
        {
            get
            {
                if (AlarmType != AlarmType.Dday || !TargetDate.HasValue)
                    return null;

                var now = DateTime.Now.Date;
                var target = TargetDate.Value.Date;
                var diff = (target - now).Days;
                return diff;
            }
        }

        /// <summary>
        /// Dday 표시 문자열 (D-30, D-1, D-day 형식, 지난 날짜는 표시하지 않음)
        /// </summary>
        [JsonIgnore]
        public string DdayDisplayString
        {
            get
            {
                if (AlarmType != AlarmType.Dday)
                    return string.Empty;

                var days = DaysRemaining;
                if (!days.HasValue)
                    return string.Empty;

                if (days.Value < 0)
                    return string.Empty; // 지난 날짜는 표시하지 않음

                if (days.Value == 0)
                    return "D-day";
                else
                    return $"D-{days.Value}";
            }
        }

        /// <summary>
        /// Dday가 지났는지 여부
        /// </summary>
        [JsonIgnore]
        public bool IsDdayPassed
        {
            get
            {
                if (AlarmType != AlarmType.Dday)
                    return false;

                var days = DaysRemaining;
                return days.HasValue && days.Value < 0;
            }
        }

        /// <summary>
        /// 다음 알람 시간을 계산합니다.
        /// </summary>
        public DateTime? GetNextAlarmTime()
        {
            // Dday 타입은 알람 트리거 대상이 아님
            if (AlarmType == AlarmType.Dday)
                return null;

            if (!IsEnabled)
                return null;

            var now = DateTime.Now;
            var alarmTime = DateTime;

            if (RepeatType == RepeatType.None)
            {
                // 반복 없음: 원래 시간이 현재 시간과 같거나 미래이면 반환
                var alarmMinute = new DateTime(alarmTime.Year, alarmTime.Month, alarmTime.Day, 
                                             alarmTime.Hour, alarmTime.Minute, 0);
                var nowMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                
                // 같은 분이거나 미래이면 반환
                // 단, LastTriggered가 설정되어 있으면 이미 울린 알람이므로 null 반환
                if (alarmMinute >= nowMinute)
                {
                    // 아직 울리지 않았거나, 같은 분에 다시 울려야 하는 경우
                    if (LastTriggered == null || LastTriggered.Value < alarmMinute)
                    {
                        return alarmMinute;
                    }
                }
                return null;
            }

            // 반복이 있는 경우
            switch (RepeatType)
            {
                case RepeatType.Daily:
                    // 오늘의 알람 시간으로 설정 (분 단위로 비교)
                    var nowMinuteDaily = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                    var todayAlarm = new DateTime(now.Year, now.Month, now.Day, 
                                                 alarmTime.Hour, alarmTime.Minute, 0);
                    
                    // 오늘 알람 시간(분)이 지났거나, 이미 트리거된 경우 내일로
                    if (todayAlarm < nowMinuteDaily || 
                        (LastTriggered.HasValue && LastTriggered.Value >= todayAlarm))
                        todayAlarm = todayAlarm.AddDays(1);
                    
                    return todayAlarm;

                case RepeatType.Weekly:
                    // 선택된 요일이 있으면 해당 요일로, 없으면 원래 요일로
                    var selectedDays = SelectedDaysOfWeek?.Any() == true 
                        ? SelectedDaysOfWeek 
                        : new List<DayOfWeek> { alarmTime.DayOfWeek };
                    
                    // 오늘부터 7일 내에서 다음 알람 요일 찾기 (분 단위로 비교)
                    var nowMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                    var nextWeeklyAlarm = new DateTime(now.Year, now.Month, now.Day, 
                                                       alarmTime.Hour, alarmTime.Minute, 0);
                    
                    // 오늘 알람 시간(분)이 지났거나, 이미 트리거된 경우 내일부터 시작
                    if (nextWeeklyAlarm < nowMinute || 
                        (LastTriggered.HasValue && LastTriggered.Value >= nextWeeklyAlarm))
                        nextWeeklyAlarm = nextWeeklyAlarm.AddDays(1);
                    
                    // 7일 내에서 선택된 요일 찾기
                    for (int i = 0; i < 7; i++)
                    {
                        var checkDate = nextWeeklyAlarm.AddDays(i);
                        if (selectedDays.Contains(checkDate.DayOfWeek))
                        {
                            return checkDate;
                        }
                    }
                    // 7일 내에 없으면 다음 주로
                    return nextWeeklyAlarm.AddDays(7);

                case RepeatType.Monthly:
                    var nowMinuteMonthly = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                    
                    // 매월 알람 날짜로 설정 (원래 알람의 날짜 사용, 날짜 오버플로우 방지)
                    var targetDay = Math.Min(alarmTime.Day, DateTime.DaysInMonth(now.Year, now.Month));
                    var monthlyAlarm = new DateTime(now.Year, now.Month, targetDay,
                                                    alarmTime.Hour, alarmTime.Minute, 0);
                    
                    // 오늘 알람 시간(분)이 지났거나, 이미 트리거된 경우 다음 달로
                    if (monthlyAlarm < nowMinuteMonthly || 
                        (LastTriggered.HasValue && LastTriggered.Value >= monthlyAlarm))
                    {
                        // 다음 달 계산 (날짜 오버플로우 방지)
                        var nextMonth = now.AddMonths(1);
                        var validDay = Math.Min(alarmTime.Day, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                        monthlyAlarm = new DateTime(nextMonth.Year, nextMonth.Month, validDay,
                                                   alarmTime.Hour, alarmTime.Minute, 0);
                    }
                    
                    return monthlyAlarm;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 알람 시간 문자열 표현
        /// </summary>
        public string TimeString => DateTime.ToString("HH:mm");

        /// <summary>
        /// 반복 타입 문자열 표현
        /// </summary>
        public string RepeatTypeString
        {
            get
            {
                return RepeatType switch
                {
                    RepeatType.None => "없음",
                    RepeatType.Daily => "매일",
                    RepeatType.Weekly => "매주",
                    RepeatType.Monthly => "매월",
                    _ => "없음"
                };
            }
        }
    }
}

