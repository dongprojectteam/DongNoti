using System;
using System.Collections.Generic;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class FocusModeServiceTests
    {
        #region GetRemainingTime Logic Tests

        [Fact]
        public void GetRemainingTime_WithFutureEndTime_ReturnsPositiveTimeSpan()
        {
            // Arrange
            var endTime = DateTime.Now.AddMinutes(30);
            var remaining = endTime - DateTime.Now;

            // Act & Assert
            remaining.Should().BeGreaterThan(TimeSpan.Zero);
            remaining.TotalMinutes.Should().BeApproximately(30, 1); // 1분 오차 허용
        }

        [Fact]
        public void GetRemainingTime_WithPastEndTime_ReturnsZero()
        {
            // Arrange
            var endTime = DateTime.Now.AddMinutes(-10);
            var remaining = endTime - DateTime.Now;

            // Act & Assert
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            remaining.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void GetRemainingTime_Logic_HandlesNullEndTime()
        {
            // Arrange
            DateTime? endTime = null;
            TimeSpan remaining;

            // Act
            if (endTime.HasValue)
            {
                remaining = endTime.Value - DateTime.Now;
                remaining = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            else
            {
                remaining = TimeSpan.Zero;
            }

            // Assert
            remaining.Should().Be(TimeSpan.Zero);
        }

        #endregion

        #region GetPresets Logic Tests

        [Fact]
        public void GetPresets_EmptyPresets_ReturnsDefaultPresets()
        {
            // Arrange
            var presets = new List<FocusModePreset>();

            // Act
            var result = presets.Count == 0 
                ? AppSettings.GetDefaultPresets() 
                : presets;

            // Assert
            result.Should().NotBeEmpty();
            result.Should().HaveCount(6);
        }

        [Fact]
        public void GetPresets_WithPresets_ReturnsPresets()
        {
            // Arrange
            var presets = new List<FocusModePreset>
            {
                new FocusModePreset("custom1", "Custom 1", 45),
                new FocusModePreset("custom2", "Custom 2", 90)
            };

            // Act
            var result = presets.Count == 0 
                ? AppSettings.GetDefaultPresets() 
                : presets;

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be("custom1");
        }

        #endregion

        #region GetDefaultPreset Logic Tests

        [Fact]
        public void GetDefaultPreset_FindsPresetById()
        {
            // Arrange
            var presets = AppSettings.GetDefaultPresets();
            var defaultPresetId = "1h";

            // Act
            var defaultPreset = presets.FirstOrDefault(p => p.Id == defaultPresetId);
            var result = defaultPreset ?? presets.FirstOrDefault();

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("1h");
        }

        [Fact]
        public void GetDefaultPreset_NotFound_ReturnsFirstPreset()
        {
            // Arrange
            var presets = AppSettings.GetDefaultPresets();
            var defaultPresetId = "nonexistent";

            // Act
            var defaultPreset = presets.FirstOrDefault(p => p.Id == defaultPresetId);
            var result = defaultPreset ?? presets.FirstOrDefault();

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("30m"); // 첫 번째 프리셋
        }

        #endregion

        #region RecordMissedAlarm Logic Tests

        [Fact]
        public void RecordMissedAlarm_PreventsDuplicates()
        {
            // Arrange
            var missedAlarms = new List<MissedAlarm>
            {
                new MissedAlarm("alarm1", "Test Alarm", DateTime.Now, "없음")
            };
            var alarmId = "alarm1";

            // Act
            var isDuplicate = missedAlarms.Any(m => m.AlarmId == alarmId);

            // Assert
            isDuplicate.Should().BeTrue();
        }

        [Fact]
        public void RecordMissedAlarm_AllowsNewAlarm()
        {
            // Arrange
            var missedAlarms = new List<MissedAlarm>
            {
                new MissedAlarm("alarm1", "Test Alarm 1", DateTime.Now, "없음")
            };
            var alarmId = "alarm2";

            // Act
            var isDuplicate = missedAlarms.Any(m => m.AlarmId == alarmId);

            // Assert
            isDuplicate.Should().BeFalse();
        }

        #endregion

        #region IsFocusModeActive Logic Tests

        [Fact]
        public void IsFocusModeActive_WithFutureEndTime_ReturnsTrue()
        {
            // Arrange
            var isActive = true;
            DateTime? endTime = DateTime.Now.AddMinutes(30);

            // Act
            var result = isActive && endTime.HasValue && endTime.Value > DateTime.Now;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsFocusModeActive_WithPastEndTime_ReturnsFalse()
        {
            // Arrange
            var isActive = true;
            DateTime? endTime = DateTime.Now.AddMinutes(-10);

            // Act
            var result = isActive && endTime.HasValue && endTime.Value > DateTime.Now;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsFocusModeActive_WhenInactive_ReturnsFalse()
        {
            // Arrange
            var isActive = false;
            DateTime? endTime = DateTime.Now.AddMinutes(30);

            // Act
            var result = isActive && endTime.HasValue && endTime.Value > DateTime.Now;

            // Assert
            result.Should().BeFalse();
        }

        #endregion
    }
}
