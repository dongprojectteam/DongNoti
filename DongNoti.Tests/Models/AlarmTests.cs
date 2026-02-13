using System;
using System.Collections.Generic;
using DongNoti.Models;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Models
{
    public class AlarmTests
    {
        #region Constructor & Default Values

        [Fact]
        public void Alarm_DefaultValues_AreCorrect()
        {
            var alarm = new Alarm();

            alarm.Id.Should().NotBeNullOrEmpty();
            alarm.Title.Should().Be("알람");
            alarm.IsEnabled.Should().BeTrue();
            alarm.RepeatType.Should().Be(RepeatType.None);
            alarm.IsTemporary.Should().BeFalse();
            alarm.AutoDismissMinutes.Should().Be(1);
            alarm.AlarmType.Should().Be(AlarmType.Alarm);
            alarm.Priority.Should().Be(Priority.Normal);
            alarm.Category.Should().BeNull();
            alarm.CategoryDisplay.Should().Be("기본");
        }

        #endregion

        #region GetNextAlarmTime - RepeatType.None

        [Fact]
        public void GetNextAlarmTime_NoRepeat_FutureTime_ReturnsAlarmTime()
        {
            var futureTime = DateTime.Now.AddHours(1);
            var alarm = new Alarm
            {
                DateTime = futureTime,
                RepeatType = RepeatType.None,
                IsEnabled = true
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().NotBeNull();
            result!.Value.Hour.Should().Be(futureTime.Hour);
            result!.Value.Minute.Should().Be(futureTime.Minute);
        }

        [Fact]
        public void GetNextAlarmTime_NoRepeat_PastTime_ReturnsNull()
        {
            var pastTime = DateTime.Now.AddHours(-1);
            var alarm = new Alarm
            {
                DateTime = pastTime,
                RepeatType = RepeatType.None,
                IsEnabled = true
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().BeNull();
        }

        [Fact]
        public void GetNextAlarmTime_NoRepeat_AlreadyTriggered_ReturnsNull()
        {
            var alarmTime = DateTime.Now.AddMinutes(5);
            var alarm = new Alarm
            {
                DateTime = alarmTime,
                RepeatType = RepeatType.None,
                IsEnabled = true,
                LastTriggered = alarmTime // Already triggered
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().BeNull();
        }

        [Fact]
        public void GetNextAlarmTime_Disabled_ReturnsNull()
        {
            var alarm = new Alarm
            {
                DateTime = DateTime.Now.AddHours(1),
                RepeatType = RepeatType.None,
                IsEnabled = false
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().BeNull();
        }

        #endregion

        #region GetNextAlarmTime - RepeatType.Daily

        [Fact]
        public void GetNextAlarmTime_Daily_FutureTimeToday_ReturnsTodayAlarm()
        {
            var now = DateTime.Now;
            var futureTimeToday = now.AddHours(2);
            var alarm = new Alarm
            {
                DateTime = futureTimeToday,
                RepeatType = RepeatType.Daily,
                IsEnabled = true
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().NotBeNull();
            result!.Value.Date.Should().Be(now.Date);
            result!.Value.Hour.Should().Be(futureTimeToday.Hour);
            result!.Value.Minute.Should().Be(futureTimeToday.Minute);
        }

        [Fact]
        public void GetNextAlarmTime_Daily_PastTimeToday_ReturnsTomorrowAlarm()
        {
            var now = DateTime.Now;
            var pastTimeToday = now.AddHours(-2);
            var alarm = new Alarm
            {
                DateTime = pastTimeToday,
                RepeatType = RepeatType.Daily,
                IsEnabled = true
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().NotBeNull();
            result!.Value.Date.Should().Be(now.Date.AddDays(1));
        }

        #endregion

        #region GetNextAlarmTime - RepeatType.Weekly

        [Fact]
        public void GetNextAlarmTime_Weekly_WithSelectedDays_ReturnsNextSelectedDay()
        {
            var now = DateTime.Now;
            var alarm = new Alarm
            {
                DateTime = now.AddHours(2),
                RepeatType = RepeatType.Weekly,
                IsEnabled = true,
                SelectedDaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().NotBeNull();
            result!.Value.DayOfWeek.Should().BeOneOf(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
        }

        [Fact]
        public void GetNextAlarmTime_Weekly_NoSelectedDays_UsesOriginalDayOfWeek()
        {
            var now = DateTime.Now;
            var alarm = new Alarm
            {
                DateTime = now.AddHours(2),
                RepeatType = RepeatType.Weekly,
                IsEnabled = true,
                SelectedDaysOfWeek = new List<DayOfWeek>() // Empty
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().NotBeNull();
        }

        #endregion

        #region GetNextAlarmTime - RepeatType.Monthly

        [Fact]
        public void GetNextAlarmTime_Monthly_FutureTimeThisMonth_ReturnsThisMonthAlarm()
        {
            var now = DateTime.Now;
            var futureDay = Math.Min(now.Day + 5, DateTime.DaysInMonth(now.Year, now.Month));
            var alarm = new Alarm
            {
                DateTime = new DateTime(now.Year, now.Month, futureDay, 10, 0, 0),
                RepeatType = RepeatType.Monthly,
                IsEnabled = true
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().NotBeNull();
        }

        [Fact]
        public void GetNextAlarmTime_Monthly_Day31_February_HandlesOverflow()
        {
            var alarm = new Alarm
            {
                DateTime = new DateTime(2024, 1, 31, 10, 0, 0),
                RepeatType = RepeatType.Monthly,
                IsEnabled = true
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().NotBeNull();
        }

        #endregion

        #region GetNextAlarmTime - RepeatType.Yearly

        [Fact]
        public void GetNextAlarmTime_Yearly_ReturnsNextOccurrence()
        {
            var alarm = new Alarm
            {
                DateTime = new DateTime(2024, 3, 15, 10, 0, 0),
                RepeatType = RepeatType.Yearly,
                IsEnabled = true
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().NotBeNull();
            result!.Value.Month.Should().Be(3);
            result.Value.Day.Should().Be(15);
            result.Value.Hour.Should().Be(10);
            result.Value.Minute.Should().Be(0);
        }

        #endregion

        #region GetNextAlarmTime - Dday Type

        [Fact]
        public void GetNextAlarmTime_DdayType_ReturnsNull()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                DateTime = DateTime.Now.AddDays(30),
                TargetDate = DateTime.Now.AddDays(30),
                IsEnabled = true
            };

            var result = alarm.GetNextAlarmTime();

            result.Should().BeNull(); // Dday type should not trigger alarms
        }

        #endregion

        #region DaysRemaining

        [Fact]
        public void DaysRemaining_DdayType_FutureDate_ReturnsPositiveDays()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(10)
            };

            var result = alarm.DaysRemaining;

            result.Should().Be(10);
        }

        [Fact]
        public void DaysRemaining_DdayType_Today_ReturnsZero()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date
            };

            var result = alarm.DaysRemaining;

            result.Should().Be(0);
        }

        [Fact]
        public void DaysRemaining_DdayType_PastDate_ReturnsNegativeDays()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-5)
            };

            var result = alarm.DaysRemaining;

            result.Should().Be(-5);
        }

        [Fact]
        public void DaysRemaining_AlarmType_ReturnsNull()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Alarm,
                TargetDate = DateTime.Now.AddDays(10)
            };

            var result = alarm.DaysRemaining;

            result.Should().BeNull();
        }

        [Fact]
        public void DaysRemaining_DdayType_NoTargetDate_ReturnsNull()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = null
            };

            var result = alarm.DaysRemaining;

            result.Should().BeNull();
        }

        [Fact]
        public void DaysRemaining_DdayType_Yearly_PastDateThisYear_ReturnsDaysUntilNextOccurrence()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = new DateTime(2000, 3, 15), // 월/일만 사용
                RepeatType = RepeatType.Yearly
            };
            var result = alarm.DaysRemaining;
            result.Should().NotBeNull();
            result.Should().BeGreaterThanOrEqualTo(0);
        }

        #endregion

        #region DdayDisplayString

        [Fact]
        public void DdayDisplayString_DdayType_FutureDate_ReturnsDMinusFormat()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(30)
            };

            var result = alarm.DdayDisplayString;

            result.Should().Be("D-30");
        }

        [Fact]
        public void DdayDisplayString_DdayType_Today_ReturnsDday()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date
            };

            var result = alarm.DdayDisplayString;

            result.Should().Be("D-day");
        }

        [Fact]
        public void DdayDisplayString_DdayType_PastDate_ReturnsDPlusFormat()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-5)
            };

            var result = alarm.DdayDisplayString;

            result.Should().Be("D+5");
        }

        [Fact]
        public void DdayDisplayString_AlarmType_ReturnsEmpty()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Alarm
            };

            var result = alarm.DdayDisplayString;

            result.Should().BeEmpty();
        }

        #endregion

        #region DdayYearsPassedDisplay

        [Fact]
        public void DdayYearsPassedDisplay_DdayType_365DaysPast_ReturnsOneYearPassed()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-365)
            };

            var result = alarm.DdayYearsPassedDisplay;

            result.Should().Be("1년 지남");
        }

        [Fact]
        public void DdayYearsPassedDisplay_DdayType_364DaysPast_ReturnsEmpty()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-364)
            };

            var result = alarm.DdayYearsPassedDisplay;

            result.Should().BeEmpty();
        }

        [Fact]
        public void DdayYearsPassedDisplay_DdayType_730DaysPast_ReturnsTwoYearsPassed()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-730)
            };

            var result = alarm.DdayYearsPassedDisplay;

            result.Should().Be("2년 지남");
        }

        [Fact]
        public void DdayYearsPassedDisplay_AlarmType_ReturnsEmpty()
        {
            var alarm = new Alarm { AlarmType = AlarmType.Alarm };

            var result = alarm.DdayYearsPassedDisplay;

            result.Should().BeEmpty();
        }

        [Fact]
        public void DdayYearsPassedDisplay_YearlyDday_PastTargetDate_ReturnsYearsPassed()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                RepeatType = RepeatType.Yearly,
                TargetDate = DateTime.Now.Date.AddYears(-8).AddDays(-10)
            };

            var result = alarm.DdayYearsPassedDisplay;

            result.Should().Be("8년 지남");
        }

        #endregion

        #region IsDdayPassed

        [Fact]
        public void IsDdayPassed_DdayType_PastDate_ReturnsTrue()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-1)
            };

            var result = alarm.IsDdayPassed;

            result.Should().BeTrue();
        }

        [Fact]
        public void IsDdayPassed_DdayType_FutureDate_ReturnsFalse()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(1)
            };

            var result = alarm.IsDdayPassed;

            result.Should().BeFalse();
        }

        [Fact]
        public void IsDdayPassed_DdayType_Today_ReturnsFalse()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date
            };

            var result = alarm.IsDdayPassed;

            result.Should().BeFalse();
        }

        [Fact]
        public void IsDdayPassed_DdayType_Yearly_PastDate_ReturnsFalse()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-10),
                RepeatType = RepeatType.Yearly
            };

            var result = alarm.IsDdayPassed;

            result.Should().BeFalse();
        }

        [Fact]
        public void IsDdayPassed_AlarmType_ReturnsFalse()
        {
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Alarm
            };

            var result = alarm.IsDdayPassed;

            result.Should().BeFalse();
        }

        #endregion

        #region TimeString & RepeatTypeString

        [Fact]
        public void TimeString_ReturnsFormattedTime()
        {
            var alarm = new Alarm
            {
                DateTime = new DateTime(2024, 6, 15, 14, 30, 0)
            };

            var result = alarm.TimeString;

            result.Should().Be("14:30");
        }

        [Theory]
        [InlineData(RepeatType.None, "없음")]
        [InlineData(RepeatType.Daily, "매일")]
        [InlineData(RepeatType.Weekly, "매주")]
        [InlineData(RepeatType.Monthly, "매월")]
        [InlineData(RepeatType.Yearly, "매년")]
        public void RepeatTypeString_ReturnsKoreanText(RepeatType repeatType, string expected)
        {
            var alarm = new Alarm
            {
                RepeatType = repeatType
            };

            var result = alarm.RepeatTypeString;

            result.Should().Be(expected);
        }

        #endregion

        #region CategoryDisplay

        [Fact]
        public void CategoryDisplay_NullCategory_ReturnsDefault()
        {
            var alarm = new Alarm { Category = null };

            var result = alarm.CategoryDisplay;

            result.Should().Be("기본");
        }

        [Fact]
        public void CategoryDisplay_WithCategory_ReturnsCategory()
        {
            var alarm = new Alarm { Category = "업무" };

            var result = alarm.CategoryDisplay;

            result.Should().Be("업무");
        }

        #endregion

        #region PrioritySortKey

        [Theory]
        [InlineData(Priority.Low, 0)]
        [InlineData(Priority.Normal, 1)]
        [InlineData(Priority.High, 2)]
        public void PrioritySortKey_ReturnsCorrectValue(Priority priority, int expected)
        {
            var alarm = new Alarm { Priority = priority };

            var result = alarm.PrioritySortKey;

            result.Should().Be(expected);
        }

        #endregion
    }
}
