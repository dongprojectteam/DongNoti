using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DongNoti.Converters;
using DongNoti.Models;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Converters
{
    public class ConverterTests
    {
        [Fact]
        public void CategoryColorConverter_WithNullValue_ReturnsDefault()
        {
            var converter = new CategoryColorConverter();
            
            var result = converter.Convert(null!, typeof(Brush), "Background", CultureInfo.InvariantCulture);

            result.Should().BeOfType<SolidColorBrush>();
            ((SolidColorBrush)result!).Color.Should().Be(Colors.White);
        }

        [Fact]
        public void CategoryColorConverter_WithBorderThicknessParam_ReturnsThickness()
        {
            var converter = new CategoryColorConverter();
            
            var result = converter.Convert(null!, typeof(Thickness), "BorderThickness", CultureInfo.InvariantCulture);

            result.Should().BeOfType<Thickness>();
            ((Thickness)result!).Bottom.Should().Be(1);
        }

        [Fact]
        public void CategoryColorConverter_WithBadgeForegroundParam_ReturnsBrush()
        {
            var converter = new CategoryColorConverter();
            
            var result = converter.Convert(null!, typeof(Brush), "BadgeForeground", CultureInfo.InvariantCulture);

            result.Should().BeOfType<SolidColorBrush>();
        }

        [Fact]
        public void CommonConverters_BooleanToVisibilityConverter_ShouldWork()
        {
            var converter = new System.Windows.Controls.BooleanToVisibilityConverter();
            
            var visible = converter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture);
            var collapsed = converter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture);

            visible.Should().Be(Visibility.Visible);
            collapsed.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void NullToBoolConverter_ShouldReturnFalseOnlyForNull()
        {
            var converter = new NullToBoolConverter();

            converter.Convert(null!, typeof(bool), null!, CultureInfo.InvariantCulture).Should().Be(false);
            converter.Convert("value", typeof(bool), null!, CultureInfo.InvariantCulture).Should().Be(true);
            converter.Invoking(c => c.ConvertBack(true, typeof(object), null!, CultureInfo.InvariantCulture))
                .Should().Throw<NotImplementedException>();
        }

        [Fact]
        public void AlarmTypeToVisibilityConverter_ShouldMatchConfiguredTargetType()
        {
            var alarmConverter = new AlarmTypeToVisibilityConverter { TargetType = "Alarm" };
            var ddayConverter = new AlarmTypeToVisibilityConverter { TargetType = "Dday" };

            alarmConverter.Convert(AlarmType.Alarm, typeof(Visibility), null!, CultureInfo.InvariantCulture)
                .Should().Be(Visibility.Visible);
            alarmConverter.Convert(AlarmType.Dday, typeof(Visibility), null!, CultureInfo.InvariantCulture)
                .Should().Be(Visibility.Collapsed);
            ddayConverter.Convert(AlarmType.Dday, typeof(Visibility), null!, CultureInfo.InvariantCulture)
                .Should().Be(Visibility.Visible);
            ddayConverter.Convert("bad", typeof(Visibility), null!, CultureInfo.InvariantCulture)
                .Should().Be(Visibility.Collapsed);
            alarmConverter.Invoking(c => c.ConvertBack(Visibility.Visible, typeof(AlarmType), null!, CultureInfo.InvariantCulture))
                .Should().Throw<NotImplementedException>();
        }

        [Fact]
        public void IsPastAlarmConverter_ShouldHandleAlarmAndDday()
        {
            var converter = new IsPastAlarmConverter();

            var pastAlarm = new Alarm
            {
                AlarmType = AlarmType.Alarm,
                RepeatType = RepeatType.None,
                DateTime = DateTime.Now.AddDays(-1),
                LastTriggered = DateTime.Now.AddDays(-1)
            };
            var futureAlarm = new Alarm
            {
                AlarmType = AlarmType.Alarm,
                RepeatType = RepeatType.None,
                DateTime = DateTime.Now.AddDays(1)
            };
            var pastDday = new Alarm
            {
                AlarmType = AlarmType.Dday,
                TargetDate = DateTime.Today.AddDays(-1)
            };

            converter.Convert(pastAlarm, typeof(bool), null!, CultureInfo.InvariantCulture).Should().Be(true);
            converter.Convert(futureAlarm, typeof(bool), null!, CultureInfo.InvariantCulture).Should().Be(false);
            converter.Convert(pastDday, typeof(bool), null!, CultureInfo.InvariantCulture).Should().Be(true);
            converter.Convert("bad", typeof(bool), null!, CultureInfo.InvariantCulture).Should().Be(false);
            converter.Invoking(c => c.ConvertBack(false, typeof(Alarm), null!, CultureInfo.InvariantCulture))
                .Should().Throw<NotImplementedException>();
        }

        [Fact]
        public void MainWindowDdayDisplayConverter_ShouldFormatRelativeDays()
        {
            var converter = new MainWindowDdayDisplayConverter();

            converter.Convert(new Alarm { AlarmType = AlarmType.Dday, TargetDate = DateTime.Today.AddDays(-2) },
                    typeof(string), null!, CultureInfo.InvariantCulture)
                .Should().Be("D+2");
            converter.Convert(new Alarm { AlarmType = AlarmType.Dday, TargetDate = DateTime.Today },
                    typeof(string), null!, CultureInfo.InvariantCulture)
                .Should().Be("D-day");
            converter.Convert(new Alarm { AlarmType = AlarmType.Dday, TargetDate = DateTime.Today.AddDays(3) },
                    typeof(string), null!, CultureInfo.InvariantCulture)
                .Should().Be("D-3");
            converter.Convert(new Alarm { AlarmType = AlarmType.Alarm }, typeof(string), null!, CultureInfo.InvariantCulture)
                .Should().Be(string.Empty);
            converter.Invoking(c => c.ConvertBack(string.Empty, typeof(Alarm), null!, CultureInfo.InvariantCulture))
                .Should().Throw<NotImplementedException>();
        }

        [Theory]
        [InlineData("RowBackground")]
        [InlineData("BorderBrush")]
        [InlineData("BadgeBackground")]
        [InlineData("BadgeBorderBrush")]
        public void CategoryColorConverter_DefaultBrushParameters_ReturnBrush(string parameter)
        {
            var converter = new CategoryColorConverter();

            var result = converter.Convert(null!, typeof(Brush), parameter, CultureInfo.InvariantCulture);

            result.Should().BeOfType<SolidColorBrush>();
        }

        [Fact]
        public void CategoryColorConverter_UnknownParameter_ReturnsUnsetValue()
        {
            var converter = new CategoryColorConverter();

            var result = converter.Convert(null!, typeof(object), "Unknown", CultureInfo.InvariantCulture);

            result.Should().Be(DependencyProperty.UnsetValue);
        }
    }
}
