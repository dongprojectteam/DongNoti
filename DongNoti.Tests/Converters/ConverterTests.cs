using System;
using System.Globalization;
using System.Windows;
using DongNoti.Converters;
using DongNoti.Models;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Converters
{
    public class NullToBoolConverterTests
    {
        private readonly NullToBoolConverter _converter = new();

        [Fact]
        public void Convert_NullValue_ReturnsFalse()
        {
            var result = _converter.Convert(null!, typeof(bool), null!, CultureInfo.InvariantCulture);
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_NonNullValue_ReturnsTrue()
        {
            var result = _converter.Convert("test", typeof(bool), null!, CultureInfo.InvariantCulture);
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_EmptyString_ReturnsTrue()
        {
            var result = _converter.Convert(string.Empty, typeof(bool), null!, CultureInfo.InvariantCulture);
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_Object_ReturnsTrue()
        {
            var result = _converter.Convert(new object(), typeof(bool), null!, CultureInfo.InvariantCulture);
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_IntValue_ReturnsTrue()
        {
            var result = _converter.Convert(0, typeof(bool), null!, CultureInfo.InvariantCulture);
            result.Should().Be(true);
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => 
                _converter.ConvertBack(true, typeof(object), null!, CultureInfo.InvariantCulture));
        }
    }

    public class AlarmTypeToVisibilityConverterTests
    {
        [Fact]
        public void Convert_AlarmType_TargetAlarm_ReturnsVisible()
        {
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };
            var result = converter.Convert(AlarmType.Alarm, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void Convert_DdayType_TargetAlarm_ReturnsCollapsed()
        {
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };
            var result = converter.Convert(AlarmType.Dday, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_DdayType_TargetDday_ReturnsVisible()
        {
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Dday" };
            var result = converter.Convert(AlarmType.Dday, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void Convert_AlarmType_TargetDday_ReturnsCollapsed()
        {
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Dday" };
            var result = converter.Convert(AlarmType.Alarm, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_NonAlarmTypeValue_ReturnsCollapsed()
        {
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };
            var result = converter.Convert("invalid", typeof(Visibility), null!, CultureInfo.InvariantCulture);
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_NullValue_ReturnsCollapsed()
        {
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };
            var result = converter.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture);
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void DefaultTargetType_IsAlarm()
        {
            var converter = new AlarmTypeToVisibilityConverter();
            converter.TargetType.Should().Be("Alarm");
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            var converter = new AlarmTypeToVisibilityConverter();
            Assert.Throws<NotImplementedException>(() => 
                converter.ConvertBack(Visibility.Visible, typeof(AlarmType), null!, CultureInfo.InvariantCulture));
        }
    }
}
