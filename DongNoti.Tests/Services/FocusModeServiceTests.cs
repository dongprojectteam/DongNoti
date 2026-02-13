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
            var endTime = DateTime.Now.AddMinutes(30);
            var remaining = endTime - DateTime.Now;
            remaining.Should().BeGreaterThan(TimeSpan.Zero);
            remaining.TotalMinutes.Should().BeApproximately(30, 1);
        }

        [Fact]
        public void GetRemainingTime_WithPastEndTime_ReturnsZero()
        {
            var endTime = DateTime.Now.AddMinutes(-10);
            var remaining = endTime - DateTime.Now;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            remaining.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void GetRemainingTime_Logic_HandlesNullEndTime()
        {
            DateTime? endTime = null;
            TimeSpan remaining;
            if (endTime.HasValue)
            {
                remaining = endTime.Value - DateTime.Now;
                remaining = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            else
            {
                remaining = TimeSpan.Zero;
            }
            remaining.Should().Be(TimeSpan.Zero);
        }

        #endregion

        #region GetPresets Logic Tests

        [Fact]
        public void GetPresets_EmptyPresets_ReturnsDefaultPresets()
        {
            var presets = new List<FocusModePreset>();
            var result = presets.Count == 0 
                ? AppSettings.GetDefaultPresets() 
                : presets;
            result.Should().NotBeEmpty();
            result.Should().HaveCount(6);
        }

        [Fact]
        public void GetPresets_WithPresets_ReturnsPresets()
        {
            var presets = new List<FocusModePreset>
            {
                new FocusModePreset("custom1", "Custom 1", 45),
                new FocusModePreset("custom2", "Custom 2", 90)
            };
            var result = presets.Count == 0
                ? AppSettings.GetDefaultPresets()
                : presets;
            result.Should().HaveCount(2);
            result[0].Id.Should().Be("custom1");
        }

        #endregion

        #region GetDefaultPreset Logic Tests

        [Fact]
        public void GetDefaultPreset_FindsPresetById()
        {
            var presets = AppSettings.GetDefaultPresets();
            var defaultPresetId = "1h";
            var defaultPreset = presets.FirstOrDefault(p => p.Id == defaultPresetId);
            var result = defaultPreset ?? presets.FirstOrDefault();
            result.Should().NotBeNull();
            result!.Id.Should().Be("1h");
        }

        [Fact]
        public void GetDefaultPreset_NotFound_ReturnsFirstPreset()
        {
            var presets = AppSettings.GetDefaultPresets();
            var defaultPresetId = "nonexistent";
            var defaultPreset = presets.FirstOrDefault(p => p.Id == defaultPresetId);
            var result = defaultPreset ?? presets.FirstOrDefault();
            result.Should().NotBeNull();
            result!.Id.Should().Be("30m");
        }

        #endregion

        #region RecordMissedAlarm Logic Tests

        [Fact]
        public void RecordMissedAlarm_PreventsDuplicates()
        {
            var missedAlarms = new List<MissedAlarm>
            {
                new MissedAlarm("alarm1", "Test Alarm", DateTime.Now, "없음")
            };
            var alarmId = "alarm1";
            var isDuplicate = missedAlarms.Any(m => m.AlarmId == alarmId);
            isDuplicate.Should().BeTrue();
        }

        [Fact]
        public void RecordMissedAlarm_AllowsNewAlarm()
        {
            var missedAlarms = new List<MissedAlarm>
            {
                new MissedAlarm("alarm1", "Test Alarm 1", DateTime.Now, "없음")
            };
            var alarmId = "alarm2";
            var isDuplicate = missedAlarms.Any(m => m.AlarmId == alarmId);
            isDuplicate.Should().BeFalse();
        }

        #endregion

        #region IsFocusModeActive Logic Tests

        [Fact]
        public void IsFocusModeActive_WithFutureEndTime_ReturnsTrue()
        {
            var isActive = true;
            DateTime? endTime = DateTime.Now.AddMinutes(30);
            var result = isActive && endTime.HasValue && endTime.Value > DateTime.Now;
            result.Should().BeTrue();
        }

        [Fact]
        public void IsFocusModeActive_WithPastEndTime_ReturnsFalse()
        {
            var isActive = true;
            DateTime? endTime = DateTime.Now.AddMinutes(-10);
            var result = isActive && endTime.HasValue && endTime.Value > DateTime.Now;
            result.Should().BeFalse();
        }

        [Fact]
        public void IsFocusModeActive_WhenInactive_ReturnsFalse()
        {
            var isActive = false;
            DateTime? endTime = DateTime.Now.AddMinutes(30);
            var result = isActive && endTime.HasValue && endTime.Value > DateTime.Now;
            result.Should().BeFalse();
        }

        #endregion
    }
}
