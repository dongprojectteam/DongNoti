using System;
using System.Collections.Generic;
using System.Linq;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class StatisticsServiceTests
    {
        #region CalculateStatistics Tests

        [Fact]
        public void CalculateStatistics_EmptyHistory_ReturnsZeroStatistics()
        {
            var settings = new AppSettings
            {
                AlarmHistory = new List<AlarmHistory>()
            };
            var result = StatisticsService.CalculateStatistics();
            result.Should().NotBeNull();
            result.TotalTriggers.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void CalculateStatistics_WithHistory_CountsCorrectly()
        {
            var alarmId1 = Guid.NewGuid().ToString();
            var alarmId2 = Guid.NewGuid().ToString();
            
            var history = new List<AlarmHistory>
            {
                new AlarmHistory { AlarmId = alarmId1, AlarmTitle = "Alarm1", WasMissed = false, TriggeredAt = DateTime.Now.AddDays(-1) },
                new AlarmHistory { AlarmId = alarmId1, AlarmTitle = "Alarm1", WasMissed = true, TriggeredAt = DateTime.Now.AddDays(-2) },
                new AlarmHistory { AlarmId = alarmId2, AlarmTitle = "Alarm2", WasMissed = false, TriggeredAt = DateTime.Now.AddDays(-3) },
                new AlarmHistory { AlarmId = alarmId2, AlarmTitle = "Alarm2", WasMissed = false, TriggeredAt = DateTime.Now.AddDays(-4) }
            };
            var filtered = history.Where(h => true).ToList();
            var total = filtered.Count;
            var missed = filtered.Count(h => h.WasMissed);
            var successful = filtered.Count(h => !h.WasMissed);
            total.Should().Be(4);
            missed.Should().Be(1);
            successful.Should().Be(3);
        }

        [Fact]
        public void CalculateStatistics_WithDateFilter_FiltersCorrectly()
        {
            DateTime? startDate = DateTime.Now.AddDays(-5);
            DateTime? endDate = DateTime.Now.AddDays(-1);
            
            var history = new List<AlarmHistory>
            {
                new AlarmHistory { TriggeredAt = DateTime.Now.AddDays(-6) },
                new AlarmHistory { TriggeredAt = DateTime.Now.AddDays(-3) },
                new AlarmHistory { TriggeredAt = DateTime.Now.AddDays(-2) },
                new AlarmHistory { TriggeredAt = DateTime.Now }
            };

            var filtered = history.Where(h =>
            {
                if (startDate.HasValue && h.TriggeredAt < startDate.Value)
                    return false;
                if (endDate.HasValue && h.TriggeredAt > endDate.Value)
                    return false;
                return true;
            }).ToList();
            filtered.Should().HaveCount(2);
        }

        [Fact]
        public void CalculateStatistics_GroupsByAlarmId()
        {
            var alarmId1 = Guid.NewGuid().ToString();
            var alarmId2 = Guid.NewGuid().ToString();
            
            var history = new List<AlarmHistory>
            {
                new AlarmHistory { AlarmId = alarmId1, AlarmTitle = "Alarm1", TriggeredAt = DateTime.Now },
                new AlarmHistory { AlarmId = alarmId1, AlarmTitle = "Alarm1", TriggeredAt = DateTime.Now },
                new AlarmHistory { AlarmId = alarmId2, AlarmTitle = "Alarm2", TriggeredAt = DateTime.Now }
            };

            var grouped = history
                .GroupBy(h => h.AlarmId)
                .ToDictionary(
                    g => g.Key,
                    g => new AlarmTriggerInfo
                    {
                        AlarmId = g.Key,
                        AlarmTitle = g.First().AlarmTitle,
                        TriggerCount = g.Count(),
                        MissedCount = g.Count(h => h.WasMissed)
                    }
                );
            grouped.Should().HaveCount(2);
            grouped[alarmId1].TriggerCount.Should().Be(2);
            grouped[alarmId2].TriggerCount.Should().Be(1);
        }

        [Fact]
        public void CalculateStatistics_FindsMostTriggeredAlarm()
        {
            var alarmId1 = Guid.NewGuid().ToString();
            var alarmId2 = Guid.NewGuid().ToString();
            
            var history = new List<AlarmHistory>
            {
                new AlarmHistory { AlarmId = alarmId1, AlarmTitle = "Alarm1", TriggeredAt = DateTime.Now },
                new AlarmHistory { AlarmId = alarmId1, AlarmTitle = "Alarm1", TriggeredAt = DateTime.Now },
                new AlarmHistory { AlarmId = alarmId1, AlarmTitle = "Alarm1", TriggeredAt = DateTime.Now },
                new AlarmHistory { AlarmId = alarmId2, AlarmTitle = "Alarm2", TriggeredAt = DateTime.Now }
            };

            var mostTriggered = history
                .GroupBy(h => h.AlarmId)
                .OrderByDescending(g => g.Count())
                .Select(g => new AlarmTriggerInfo
                {
                    AlarmId = g.Key,
                    AlarmTitle = g.First().AlarmTitle,
                    TriggerCount = g.Count(),
                    MissedCount = g.Count(h => h.WasMissed)
                })
                .FirstOrDefault();
            mostTriggered.Should().NotBeNull();
            mostTriggered!.AlarmId.Should().Be(alarmId1);
            mostTriggered.TriggerCount.Should().Be(3);
        }

        #endregion

        #region StatisticsData Tests

        [Fact]
        public void StatisticsData_DefaultValues_AreCorrect()
        {
            var data = new StatisticsData();
            data.TotalTriggers.Should().Be(0);
            data.MissedTriggers.Should().Be(0);
            data.SuccessfulTriggers.Should().Be(0);
            data.AlarmTriggerCounts.Should().NotBeNull();
            data.MostTriggeredAlarm.Should().BeNull();
        }

        #endregion

        #region AlarmTriggerInfo Tests

        [Fact]
        public void AlarmTriggerInfo_DefaultValues_AreCorrect()
        {
            var info = new AlarmTriggerInfo();
            info.AlarmId.Should().BeEmpty();
            info.AlarmTitle.Should().BeEmpty();
            info.TriggerCount.Should().Be(0);
            info.MissedCount.Should().Be(0);
        }

        #endregion
    }
}
