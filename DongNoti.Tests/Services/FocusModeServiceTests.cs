using System;
using System.Linq;
using System.Threading;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class FocusModeServiceTests
    {
        [Fact]
        public void StartFocusMode_SetsActiveStateAndEndTime()
        {
            var service = FocusModeService.Instance;
            int durationMinutes = 30;

            service.StartFocusMode(durationMinutes);

            service.IsFocusModeActive.Should().BeTrue();
            service.EndTime.Should().BeAfter(DateTime.Now);
            service.EndTime.Should().BeBefore(DateTime.Now.AddMinutes(durationMinutes + 1));
        }

        [Fact]
        public void StopFocusMode_ResetsState()
        {
            var service = FocusModeService.Instance;
            service.StartFocusMode(10);

            service.StopFocusMode();

            service.IsFocusModeActive.Should().BeFalse();
        }

        [Fact]
        public void RecordMissedAlarm_AddsToQueue()
        {
            var service = FocusModeService.Instance;
            service.ClearMissedAlarms();
            var alarm = new Alarm { Id = "m-1", Title = "Missed" };

            service.RecordMissedAlarm(alarm);

            var missed = service.GetMissedAlarms();
            missed.Should().HaveCount(1);
            missed[0].AlarmId.Should().Be("m-1");
        }

        [Fact]
        public void GetRemainingTime_ReturnsCorrectTimeSpan()
        {
            var service = FocusModeService.Instance;
            service.StartFocusMode(60);

            var remaining = service.GetRemainingTime();

            remaining.TotalMinutes.Should().BeInRange(59, 60);
        }

        [Fact]
        public void Presets_ShouldContainDefaultValues()
        {
            var service = FocusModeService.Instance;
            var presets = service.GetPresets();

            presets.Should().NotBeEmpty();
            presets.Any(p => p.Minutes == 30).Should().BeTrue();
            presets.Any(p => p.Minutes == 60).Should().BeTrue();
        }
    }
}
