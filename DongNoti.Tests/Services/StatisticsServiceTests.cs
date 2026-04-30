using System;
using System.Collections.Generic;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class StatisticsServiceTests
    {
        [Fact]
        public void CalculateStatistics_WithEmptyHistory_ReturnsEmptyStats()
        {
            var settings = StorageService.LoadSettings();
            settings.AlarmHistory = new List<AlarmHistory>();
            StorageService.SaveSettings(settings);
            
            var stats = StatisticsService.CalculateStatistics(null, null);

            stats.Should().NotBeNull();
            stats.TotalTriggers.Should().Be(0);
        }

        [Fact]
        public void CalculateStatistics_WithDateRange_FiltersCorrectly()
        {
            var now = DateTime.Now;
            var settings = StorageService.LoadSettings();
            settings.AlarmHistory = new List<AlarmHistory>
            {
                new AlarmHistory { TriggeredAt = now.AddDays(-10), WasMissed = false },
                new AlarmHistory { TriggeredAt = now.AddDays(-5), WasMissed = false },
                new AlarmHistory { TriggeredAt = now, WasMissed = true }
            };
            StorageService.SaveSettings(settings);

            // 최근 7일간 통계 계산
            var stats = StatisticsService.CalculateStatistics(now.AddDays(-7), now);

            stats.TotalTriggers.Should().Be(2);
            stats.MissedTriggers.Should().Be(1);
        }

        [Fact]
        public void CalculateStatistics_SuccessfulTriggers_CalculatesCorrectly()
        {
            var now = DateTime.Now;
            var settings = StorageService.LoadSettings();
            settings.AlarmHistory = new List<AlarmHistory>
            {
                new AlarmHistory { TriggeredAt = now, WasMissed = false },
                new AlarmHistory { TriggeredAt = now, WasMissed = true }
            };
            StorageService.SaveSettings(settings);

            var stats = StatisticsService.CalculateStatistics(null, null);

            stats.SuccessfulTriggers.Should().Be(1);
            stats.TotalTriggers.Should().Be(2);
        }
    }
}
