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
            // Act
            var result = _converter.Convert(null!, typeof(bool), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_NonNullValue_ReturnsTrue()
        {
            // Act
            var result = _converter.Convert("test", typeof(bool), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_EmptyString_ReturnsTrue()
        {
            // Act
            var result = _converter.Convert(string.Empty, typeof(bool), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(true); // Empty string is not null
        }

        [Fact]
        public void Convert_Object_ReturnsTrue()
        {
            // Act
            var result = _converter.Convert(new object(), typeof(bool), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_IntValue_ReturnsTrue()
        {
            // Act
            var result = _converter.Convert(0, typeof(bool), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(true); // 0 is not null
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            // Act & Assert
            Assert.Throws<NotImplementedException>(() => 
                _converter.ConvertBack(true, typeof(object), null!, CultureInfo.InvariantCulture));
        }
    }

    public class AlarmTypeToVisibilityConverterTests
    {
        [Fact]
        public void Convert_AlarmType_TargetAlarm_ReturnsVisible()
        {
            // Arrange
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };

            // Act
            var result = converter.Convert(AlarmType.Alarm, typeof(Visibility), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void Convert_DdayType_TargetAlarm_ReturnsCollapsed()
        {
            // Arrange
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };

            // Act
            var result = converter.Convert(AlarmType.Dday, typeof(Visibility), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_DdayType_TargetDday_ReturnsVisible()
        {
            // Arrange
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Dday" };

            // Act
            var result = converter.Convert(AlarmType.Dday, typeof(Visibility), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void Convert_AlarmType_TargetDday_ReturnsCollapsed()
        {
            // Arrange
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Dday" };

            // Act
            var result = converter.Convert(AlarmType.Alarm, typeof(Visibility), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_NonAlarmTypeValue_ReturnsCollapsed()
        {
            // Arrange
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };

            // Act
            var result = converter.Convert("invalid", typeof(Visibility), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_NullValue_ReturnsCollapsed()
        {
            // Arrange
            var converter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };

            // Act
            var result = converter.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void DefaultTargetType_IsAlarm()
        {
            // Arrange
            var converter = new AlarmTypeToVisibilityConverter();

            // Assert
            converter.TargetType.Should().Be("Alarm");
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            // Arrange
            var converter = new AlarmTypeToVisibilityConverter();

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => 
                converter.ConvertBack(Visibility.Visible, typeof(AlarmType), null!, CultureInfo.InvariantCulture));
        }
    }
}
