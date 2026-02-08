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
            // Act
            var alarm = new Alarm();

            // Assert
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
            // Arrange
            var futureTime = DateTime.Now.AddHours(1);
            var alarm = new Alarm
            {
                DateTime = futureTime,
                RepeatType = RepeatType.None,
                IsEnabled = true
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().NotBeNull();
            result!.Value.Hour.Should().Be(futureTime.Hour);
            result!.Value.Minute.Should().Be(futureTime.Minute);
        }

        [Fact]
        public void GetNextAlarmTime_NoRepeat_PastTime_ReturnsNull()
        {
            // Arrange
            var pastTime = DateTime.Now.AddHours(-1);
            var alarm = new Alarm
            {
                DateTime = pastTime,
                RepeatType = RepeatType.None,
                IsEnabled = true
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetNextAlarmTime_NoRepeat_AlreadyTriggered_ReturnsNull()
        {
            // Arrange
            var alarmTime = DateTime.Now.AddMinutes(5);
            var alarm = new Alarm
            {
                DateTime = alarmTime,
                RepeatType = RepeatType.None,
                IsEnabled = true,
                LastTriggered = alarmTime // Already triggered
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetNextAlarmTime_Disabled_ReturnsNull()
        {
            // Arrange
            var alarm = new Alarm
            {
                DateTime = DateTime.Now.AddHours(1),
                RepeatType = RepeatType.None,
                IsEnabled = false
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetNextAlarmTime - RepeatType.Daily

        [Fact]
        public void GetNextAlarmTime_Daily_FutureTimeToday_ReturnsTodayAlarm()
        {
            // Arrange
            var now = DateTime.Now;
            var futureTimeToday = now.AddHours(2);
            var alarm = new Alarm
            {
                DateTime = futureTimeToday,
                RepeatType = RepeatType.Daily,
                IsEnabled = true
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().NotBeNull();
            result!.Value.Date.Should().Be(now.Date);
            result!.Value.Hour.Should().Be(futureTimeToday.Hour);
            result!.Value.Minute.Should().Be(futureTimeToday.Minute);
        }

        [Fact]
        public void GetNextAlarmTime_Daily_PastTimeToday_ReturnsTomorrowAlarm()
        {
            // Arrange
            var now = DateTime.Now;
            var pastTimeToday = now.AddHours(-2);
            var alarm = new Alarm
            {
                DateTime = pastTimeToday,
                RepeatType = RepeatType.Daily,
                IsEnabled = true
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().NotBeNull();
            result!.Value.Date.Should().Be(now.Date.AddDays(1));
        }

        #endregion

        #region GetNextAlarmTime - RepeatType.Weekly

        [Fact]
        public void GetNextAlarmTime_Weekly_WithSelectedDays_ReturnsNextSelectedDay()
        {
            // Arrange
            var now = DateTime.Now;
            var alarm = new Alarm
            {
                DateTime = now.AddHours(2),
                RepeatType = RepeatType.Weekly,
                IsEnabled = true,
                SelectedDaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().NotBeNull();
            result!.Value.DayOfWeek.Should().BeOneOf(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);
        }

        [Fact]
        public void GetNextAlarmTime_Weekly_NoSelectedDays_UsesOriginalDayOfWeek()
        {
            // Arrange
            var now = DateTime.Now;
            var alarm = new Alarm
            {
                DateTime = now.AddHours(2),
                RepeatType = RepeatType.Weekly,
                IsEnabled = true,
                SelectedDaysOfWeek = new List<DayOfWeek>() // Empty
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region GetNextAlarmTime - RepeatType.Monthly

        [Fact]
        public void GetNextAlarmTime_Monthly_FutureTimeThisMonth_ReturnsThisMonthAlarm()
        {
            // Arrange
            var now = DateTime.Now;
            // Set alarm for a future day this month (if possible)
            var futureDay = Math.Min(now.Day + 5, DateTime.DaysInMonth(now.Year, now.Month));
            var alarm = new Alarm
            {
                DateTime = new DateTime(now.Year, now.Month, futureDay, 10, 0, 0),
                RepeatType = RepeatType.Monthly,
                IsEnabled = true
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void GetNextAlarmTime_Monthly_Day31_February_HandlesOverflow()
        {
            // Arrange - Create alarm for 31st
            var alarm = new Alarm
            {
                DateTime = new DateTime(2024, 1, 31, 10, 0, 0),
                RepeatType = RepeatType.Monthly,
                IsEnabled = true
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().NotBeNull();
            // Should not throw and should return valid date
        }

        #endregion

        #region GetNextAlarmTime - Dday Type

        [Fact]
        public void GetNextAlarmTime_DdayType_ReturnsNull()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                DateTime = DateTime.Now.AddDays(30),
                TargetDate = DateTime.Now.AddDays(30),
                IsEnabled = true
            };

            // Act
            var result = alarm.GetNextAlarmTime();

            // Assert
            result.Should().BeNull(); // Dday type should not trigger alarms
        }

        #endregion

        #region DaysRemaining

        [Fact]
        public void DaysRemaining_DdayType_FutureDate_ReturnsPositiveDays()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(10)
            };

            // Act
            var result = alarm.DaysRemaining;

            // Assert
            result.Should().Be(10);
        }

        [Fact]
        public void DaysRemaining_DdayType_Today_ReturnsZero()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date
            };

            // Act
            var result = alarm.DaysRemaining;

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public void DaysRemaining_DdayType_PastDate_ReturnsNegativeDays()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-5)
            };

            // Act
            var result = alarm.DaysRemaining;

            // Assert
            result.Should().Be(-5);
        }

        [Fact]
        public void DaysRemaining_AlarmType_ReturnsNull()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Alarm,
                TargetDate = DateTime.Now.AddDays(10)
            };

            // Act
            var result = alarm.DaysRemaining;

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void DaysRemaining_DdayType_NoTargetDate_ReturnsNull()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = null
            };

            // Act
            var result = alarm.DaysRemaining;

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region DdayDisplayString

        [Fact]
        public void DdayDisplayString_DdayType_FutureDate_ReturnsDMinusFormat()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(30)
            };

            // Act
            var result = alarm.DdayDisplayString;

            // Assert
            result.Should().Be("D-30");
        }

        [Fact]
        public void DdayDisplayString_DdayType_Today_ReturnsDday()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date
            };

            // Act
            var result = alarm.DdayDisplayString;

            // Assert
            result.Should().Be("D-day");
        }

        [Fact]
        public void DdayDisplayString_DdayType_PastDate_ReturnsEmpty()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-5)
            };

            // Act
            var result = alarm.DdayDisplayString;

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DdayDisplayString_AlarmType_ReturnsEmpty()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Alarm
            };

            // Act
            var result = alarm.DdayDisplayString;

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region IsDdayPassed

        [Fact]
        public void IsDdayPassed_DdayType_PastDate_ReturnsTrue()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(-1)
            };

            // Act
            var result = alarm.IsDdayPassed;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsDdayPassed_DdayType_FutureDate_ReturnsFalse()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date.AddDays(1)
            };

            // Act
            var result = alarm.IsDdayPassed;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsDdayPassed_DdayType_Today_ReturnsFalse()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Now.Date
            };

            // Act
            var result = alarm.IsDdayPassed;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsDdayPassed_AlarmType_ReturnsFalse()
        {
            // Arrange
            var alarm = new Alarm
            {
                AlarmType = AlarmType.Alarm
            };

            // Act
            var result = alarm.IsDdayPassed;

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region TimeString & RepeatTypeString

        [Fact]
        public void TimeString_ReturnsFormattedTime()
        {
            // Arrange
            var alarm = new Alarm
            {
                DateTime = new DateTime(2024, 6, 15, 14, 30, 0)
            };

            // Act
            var result = alarm.TimeString;

            // Assert
            result.Should().Be("14:30");
        }

        [Theory]
        [InlineData(RepeatType.None, "없음")]
        [InlineData(RepeatType.Daily, "매일")]
        [InlineData(RepeatType.Weekly, "매주")]
        [InlineData(RepeatType.Monthly, "매월")]
        public void RepeatTypeString_ReturnsKoreanText(RepeatType repeatType, string expected)
        {
            // Arrange
            var alarm = new Alarm
            {
                RepeatType = repeatType
            };

            // Act
            var result = alarm.RepeatTypeString;

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region CategoryDisplay

        [Fact]
        public void CategoryDisplay_NullCategory_ReturnsDefault()
        {
            // Arrange
            var alarm = new Alarm { Category = null };

            // Act
            var result = alarm.CategoryDisplay;

            // Assert
            result.Should().Be("기본");
        }

        [Fact]
        public void CategoryDisplay_WithCategory_ReturnsCategory()
        {
            // Arrange
            var alarm = new Alarm { Category = "업무" };

            // Act
            var result = alarm.CategoryDisplay;

            // Assert
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
            // Arrange
            var alarm = new Alarm { Priority = priority };

            // Act
            var result = alarm.PrioritySortKey;

            // Assert
            result.Should().Be(expected);
        }

        #endregion
    }
}
