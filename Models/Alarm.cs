using System;
using System.Collections.Generic;
using DongNoti;
using System.Linq;
using System.Text.Json.Serialization;

namespace DongNoti.Models
{
    public enum RepeatType
    {
        None,
        Daily,
        Weekly,
        Monthly,
        Yearly
    }

    public enum AlarmType
    {
        Alarm,
        Dday
    }

    public class Alarm
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "알람";
        public DateTime DateTime { get; set; } = DateTime.Now.AddHours(1);
        public RepeatType RepeatType { get; set; } = RepeatType.None;
        public bool IsEnabled { get; set; } = true;
        public string? SoundFilePath { get; set; }
        public DateTime? LastTriggered { get; set; }
        public List<DayOfWeek> SelectedDaysOfWeek { get; set; } = new List<DayOfWeek>();
        public bool IsTemporary { get; set; } = false;
        public int AutoDismissMinutes { get; set; } = 1;
        public string? Category { get; set; }
        public Priority Priority { get; set; } = Priority.Normal;
        public AlarmType AlarmType { get; set; } = AlarmType.Alarm;
        public DateTime? TargetDate { get; set; }
        public string? Memo { get; set; }
        public bool AutoRegisterAsDday { get; set; } = false;

        [JsonIgnore]
        public string CategoryDisplay => Category ?? "기본";

        [JsonIgnore]
        public int PrioritySortKey => (int)Priority;

        /// <summary>
        /// Dday 타입일 때 남은 일수 계산 (음수는 지난 날짜).
        /// 연간 반복일 때는 다음 도래일(같은 월/일) 기준으로 계산.
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

                if (RepeatType == RepeatType.Yearly)
                {
                    var nextOccurrence = new DateTime(now.Year, target.Month, Math.Min(target.Day, DateTime.DaysInMonth(now.Year, target.Month)));
                    if (nextOccurrence < now)
                        nextOccurrence = new DateTime(now.Year + 1, target.Month, Math.Min(target.Day, DateTime.DaysInMonth(now.Year + 1, target.Month)));
                    return (nextOccurrence - now).Days;
                }

                var diff = (target - now).Days;
                return diff;
            }
        }

        /// <summary>
        /// Dday 표시 문자열 (D-30, D-1, D-day, 지난 날짜는 D+n 형식)
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
                    return $"D+{Math.Abs(days.Value)}";
                if (days.Value == 0)
                    return "D-day";
                return $"D-{days.Value}";
            }
        }

        /// <summary>
        /// Dday의 목표일(TargetDate)이 1년 이상 지났을 때 "n년 지남" 형식 문자열.
        /// 연간 반복(생일 등)도 목표일 기준으로 표시.
        /// </summary>
        [JsonIgnore]
        public string DdayYearsPassedDisplay
        {
            get
            {
                if (AlarmType != AlarmType.Dday || !TargetDate.HasValue)
                    return string.Empty;
                var target = TargetDate.Value.Date;
                var today = DateTime.Now.Date;
                if (target >= today)
                    return string.Empty;
                int totalDays = (today - target).Days;
                if (totalDays < 365)
                    return string.Empty;
                int years = totalDays / 365;
                return years > 0 ? $"{years}년 지남" : string.Empty;
            }
        }

        /// <summary>
        /// Dday가 반복(연간 등)일 때 "반복" 표시용. 아니면 빈 문자열.
        /// </summary>
        [JsonIgnore]
        public string DdayRepeatDisplay =>
            AlarmType == AlarmType.Dday && RepeatType != RepeatType.None ? "반복" : string.Empty;

        /// <summary>
        /// Dday가 지났는지 여부. 연간 반복 D-day는 다음 도래일이 항상 있으므로 false.
        /// </summary>
        [JsonIgnore]
        public bool IsDdayPassed
        {
            get
            {
                if (AlarmType != AlarmType.Dday)
                    return false;
                if (RepeatType == RepeatType.Yearly)
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
            if (AlarmType == AlarmType.Dday)
                return null;

            if (!IsEnabled)
                return null;

            var now = DateTime.Now;
            var alarmTime = DateTime;

            if (RepeatType == RepeatType.None)
            {
                var alarmMinute = new DateTime(alarmTime.Year, alarmTime.Month, alarmTime.Day, 
                                             alarmTime.Hour, alarmTime.Minute, 0);
                var nowMinute = TimeHelper.ToMinutePrecision(now);
                if (alarmMinute >= nowMinute)
                {
                    if (LastTriggered == null || LastTriggered.Value < alarmMinute)
                    {
                        return alarmMinute;
                    }
                }
                return null;
            }
            switch (RepeatType)
            {
                case RepeatType.Daily:
                    var nowMinuteDaily = TimeHelper.ToMinutePrecision(now);
                    var todayAlarm = new DateTime(now.Year, now.Month, now.Day, 
                                                 alarmTime.Hour, alarmTime.Minute, 0);
                    if (todayAlarm < nowMinuteDaily || 
                        (LastTriggered.HasValue && LastTriggered.Value >= todayAlarm))
                        todayAlarm = todayAlarm.AddDays(1);
                    
                    return todayAlarm;

                case RepeatType.Weekly:
                    var selectedDays = SelectedDaysOfWeek?.Any() == true 
                        ? SelectedDaysOfWeek 
                        : new List<DayOfWeek> { alarmTime.DayOfWeek };
                    var nowMinute = TimeHelper.ToMinutePrecision(now);
                    var nextWeeklyAlarm = new DateTime(now.Year, now.Month, now.Day, 
                                                       alarmTime.Hour, alarmTime.Minute, 0);
                    if (nextWeeklyAlarm < nowMinute || 
                        (LastTriggered.HasValue && LastTriggered.Value >= nextWeeklyAlarm))
                        nextWeeklyAlarm = nextWeeklyAlarm.AddDays(1);
                    for (int i = 0; i < 7; i++)
                    {
                        var checkDate = nextWeeklyAlarm.AddDays(i);
                        if (selectedDays.Contains(checkDate.DayOfWeek))
                        {
                            return checkDate;
                        }
                    }
                    return nextWeeklyAlarm.AddDays(7);

                case RepeatType.Monthly:
                    var nowMinuteMonthly = TimeHelper.ToMinutePrecision(now);
                    var targetDay = Math.Min(alarmTime.Day, DateTime.DaysInMonth(now.Year, now.Month));
                    var monthlyAlarm = new DateTime(now.Year, now.Month, targetDay,
                                                    alarmTime.Hour, alarmTime.Minute, 0);
                    if (monthlyAlarm < nowMinuteMonthly || 
                        (LastTriggered.HasValue && LastTriggered.Value >= monthlyAlarm))
                    {
                        var nextMonth = now.AddMonths(1);
                        var validDay = Math.Min(alarmTime.Day, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                        monthlyAlarm = new DateTime(nextMonth.Year, nextMonth.Month, validDay,
                                                   alarmTime.Hour, alarmTime.Minute, 0);
                    }
                    
                    return monthlyAlarm;

                case RepeatType.Yearly:
                    var nowMinuteYearly = TimeHelper.ToMinutePrecision(now);
                    var yearTargetDay = Math.Min(alarmTime.Day, DateTime.DaysInMonth(now.Year, alarmTime.Month));
                    var yearlyAlarm = new DateTime(now.Year, alarmTime.Month, yearTargetDay,
                                                   alarmTime.Hour, alarmTime.Minute, 0);

                    if (yearlyAlarm < nowMinuteYearly ||
                        (LastTriggered.HasValue && LastTriggered.Value >= yearlyAlarm))
                    {
                        var nextYear = now.Year + 1;
                        var yearValidDay = Math.Min(alarmTime.Day, DateTime.DaysInMonth(nextYear, alarmTime.Month));
                        yearlyAlarm = new DateTime(nextYear, alarmTime.Month, yearValidDay,
                                                  alarmTime.Hour, alarmTime.Minute, 0);
                    }
                    return yearlyAlarm;

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
                    RepeatType.Yearly => "매년",
                    _ => "없음"
                };
            }
        }

        /// <summary>
        /// 객체의 얕은 복사본을 생성합니다. (팝업 큐 스냅샷용)
        /// </summary>
        public Alarm Clone()
        {
            var clone = (Alarm)this.MemberwiseClone();
            clone.SelectedDaysOfWeek = SelectedDaysOfWeek != null
                ? new List<DayOfWeek>(SelectedDaysOfWeek)
                : new List<DayOfWeek>();
            return clone;
        }
    }
}

