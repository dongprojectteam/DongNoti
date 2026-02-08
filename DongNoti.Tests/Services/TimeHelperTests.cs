using System;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class TimeHelperTests
    {
        #region FormatTimeSpan Tests

        [Fact]
        public void FormatTimeSpan_LessThanOneHour_ReturnsMinutesOnly()
        {
            // Arrange
            var timeSpan = TimeSpan.FromMinutes(30);

            // Act
            var result = TimeHelper.FormatTimeSpan(timeSpan);

            // Assert
            result.Should().Be("30분");
        }

        [Fact]
        public void FormatTimeSpan_ExactlyOneHour_ReturnsHoursAndMinutes()
        {
            // Arrange
            var timeSpan = TimeSpan.FromHours(1);

            // Act
            var result = TimeHelper.FormatTimeSpan(timeSpan);

            // Assert
            result.Should().Be("1시간 0분");
        }

        [Fact]
        public void FormatTimeSpan_OneHourAndThirtyMinutes_ReturnsHoursAndMinutes()
        {
            // Arrange
            var timeSpan = TimeSpan.FromMinutes(90);

            // Act
            var result = TimeHelper.FormatTimeSpan(timeSpan);

            // Assert
            result.Should().Be("1시간 30분");
        }

        [Fact]
        public void FormatTimeSpan_TwoHoursAndFifteenMinutes_ReturnsHoursAndMinutes()
        {
            // Arrange
            var timeSpan = new TimeSpan(2, 15, 0);

            // Act
            var result = TimeHelper.FormatTimeSpan(timeSpan);

            // Assert
            result.Should().Be("2시간 15분");
        }

        [Fact]
        public void FormatTimeSpan_ZeroMinutes_ReturnsZeroMinutes()
        {
            // Arrange
            var timeSpan = TimeSpan.Zero;

            // Act
            var result = TimeHelper.FormatTimeSpan(timeSpan);

            // Assert
            result.Should().Be("0분");
        }

        [Fact]
        public void FormatTimeSpan_FiftyNineMinutes_ReturnsMinutesOnly()
        {
            // Arrange
            var timeSpan = TimeSpan.FromMinutes(59);

            // Act
            var result = TimeHelper.FormatTimeSpan(timeSpan);

            // Assert
            result.Should().Be("59분");
        }

        [Theory]
        [InlineData(0, "0분")]
        [InlineData(1, "1분")]
        [InlineData(59, "59분")]
        [InlineData(60, "1시간 0분")]
        [InlineData(61, "1시간 1분")]
        [InlineData(120, "2시간 0분")]
        [InlineData(180, "3시간 0분")]
        public void FormatTimeSpan_VariousMinutes_ReturnsExpectedFormat(int minutes, string expected)
        {
            // Arrange
            var timeSpan = TimeSpan.FromMinutes(minutes);

            // Act
            var result = TimeHelper.FormatTimeSpan(timeSpan);

            // Assert
            result.Should().Be(expected);
        }

        #endregion

        #region ToMinutePrecision Tests

        [Fact]
        public void ToMinutePrecision_RemovesSeconds()
        {
            // Arrange
            var dateTime = new DateTime(2024, 6, 15, 14, 30, 45);

            // Act
            var result = TimeHelper.ToMinutePrecision(dateTime);

            // Assert
            result.Should().Be(new DateTime(2024, 6, 15, 14, 30, 0));
            result.Second.Should().Be(0);
        }

        [Fact]
        public void ToMinutePrecision_RemovesMilliseconds()
        {
            // Arrange
            var dateTime = new DateTime(2024, 6, 15, 14, 30, 45, 123);

            // Act
            var result = TimeHelper.ToMinutePrecision(dateTime);

            // Assert
            result.Millisecond.Should().Be(0);
        }

        [Fact]
        public void ToMinutePrecision_PreservesYearMonthDayHourMinute()
        {
            // Arrange
            var dateTime = new DateTime(2024, 12, 31, 23, 59, 59, 999);

            // Act
            var result = TimeHelper.ToMinutePrecision(dateTime);

            // Assert
            result.Year.Should().Be(2024);
            result.Month.Should().Be(12);
            result.Day.Should().Be(31);
            result.Hour.Should().Be(23);
            result.Minute.Should().Be(59);
            result.Second.Should().Be(0);
        }

        [Fact]
        public void ToMinutePrecision_AlreadyAtMinutePrecision_ReturnsSameValue()
        {
            // Arrange
            var dateTime = new DateTime(2024, 6, 15, 14, 30, 0);

            // Act
            var result = TimeHelper.ToMinutePrecision(dateTime);

            // Assert
            result.Should().Be(dateTime);
        }

        [Fact]
        public void ToMinutePrecision_MidnightWithSeconds_ReturnsMidnight()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 1, 0, 0, 59);

            // Act
            var result = TimeHelper.ToMinutePrecision(dateTime);

            // Assert
            result.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0));
        }

        #endregion
    }
}
