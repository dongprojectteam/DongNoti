using System;
using System.Collections.Generic;
using System.Linq;
using DongNoti;
using DongNoti.Models;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class AlarmServiceTests
    {
        #region RecordAlarmHistory Logic Tests

        [Fact]
        public void RecordAlarmHistory_UpdatesExistingHistoryInSameMinute()
        {
            // Arrange
            var alarmId = Guid.NewGuid().ToString();
            var now = DateTime.Now;
            var currentMinute = TimeHelper.ToMinutePrecision(now);
            
            var history = new List<AlarmHistory>
            {
                new AlarmHistory
                {
                    AlarmId = alarmId,
                    AlarmTitle = "Test Alarm",
                    TriggeredAt = currentMinute.AddSeconds(30), // 같은 분
                    WasMissed = true
                }
            };

            // Act - 같은 분에 트리거된 경우 업데이트
            var existingHistory = history
                .Where(h => h.AlarmId == alarmId)
                .OrderByDescending(h => h.TriggeredAt)
                .FirstOrDefault();

            if (existingHistory != null)
            {
                var historyMinute = TimeHelper.ToMinutePrecision(existingHistory.TriggeredAt);
                if (historyMinute == currentMinute)
                {
                    existingHistory.WasMissed = false; // 업데이트
                }
            }

            // Assert
            existingHistory.Should().NotBeNull();
            existingHistory!.WasMissed.Should().BeFalse();
        }

        [Fact]
        public void RecordAlarmHistory_CreatesNewHistoryForDifferentMinute()
        {
            // Arrange
            var alarmId = Guid.NewGuid().ToString();
            var now = DateTime.Now;
            var currentMinute = TimeHelper.ToMinutePrecision(now);
            
            var history = new List<AlarmHistory>
            {
                new AlarmHistory
                {
                    AlarmId = alarmId,
                    AlarmTitle = "Test Alarm",
                    TriggeredAt = currentMinute.AddMinutes(-1), // 다른 분
                    WasMissed = true
                }
            };

            // Act - 다른 분에 트리거된 경우 새로 추가
            var existingHistory = history
                .Where(h => h.AlarmId == alarmId)
                .OrderByDescending(h => h.TriggeredAt)
                .FirstOrDefault();

            bool shouldUpdate = false;
            if (existingHistory != null)
            {
                var historyMinute = TimeHelper.ToMinutePrecision(existingHistory.TriggeredAt);
                if (historyMinute == currentMinute)
                {
                    shouldUpdate = true;
                }
            }

            if (!shouldUpdate)
            {
                history.Add(new AlarmHistory
                {
                    AlarmId = alarmId,
                    AlarmTitle = "Test Alarm",
                    TriggeredAt = now,
                    WasMissed = false
                });
            }

            // Assert
            history.Should().HaveCount(2);
        }

        [Fact]
        public void RecordAlarmHistory_LimitsTo1000Entries()
        {
            // Arrange
            var history = new List<AlarmHistory>();
            for (int i = 0; i < 1001; i++)
            {
                history.Add(new AlarmHistory
                {
                    AlarmId = Guid.NewGuid().ToString(),
                    AlarmTitle = $"Alarm {i}",
                    TriggeredAt = DateTime.Now.AddMinutes(-i),
                    WasMissed = false
                });
            }

            // Act - 최근 1000개만 유지
            if (history.Count > 1000)
            {
                history = history
                    .OrderByDescending(h => h.TriggeredAt)
                    .Take(1000)
                    .ToList();
            }

            // Assert
            history.Should().HaveCount(1000);
        }

        #endregion

        #region CleanupExpiredTemporaryAlarms Logic Tests

        [Fact]
        public void CleanupExpiredTemporaryAlarms_RemovesPastTemporaryAlarms()
        {
            // Arrange
            var now = DateTime.Now;
            var alarms = new List<Alarm>
            {
                new Alarm { Id = "1", Title = "Past Temp", DateTime = now.AddHours(-1), IsTemporary = true },
                new Alarm { Id = "2", Title = "Future Temp", DateTime = now.AddHours(1), IsTemporary = true },
                new Alarm { Id = "3", Title = "Regular", DateTime = now.AddHours(-1), IsTemporary = false }
            };

            // Act
            var expiredTemporary = alarms
                .Where(a => a.IsTemporary && a.DateTime < now)
                .ToList();

            foreach (var alarm in expiredTemporary)
            {
                alarms.Remove(alarm);
            }

            // Assert
            alarms.Should().HaveCount(2);
            alarms.Should().NotContain(a => a.Id == "1");
            alarms.Should().Contain(a => a.Id == "2");
            alarms.Should().Contain(a => a.Id == "3");
        }

        [Fact]
        public void CleanupExpiredTemporaryAlarms_KeepsFutureTemporaryAlarms()
        {
            // Arrange
            var now = DateTime.Now;
            var alarms = new List<Alarm>
            {
                new Alarm { Id = "1", Title = "Future Temp", DateTime = now.AddHours(1), IsTemporary = true },
                new Alarm { Id = "2", Title = "Future Temp 2", DateTime = now.AddMinutes(30), IsTemporary = true }
            };

            // Act
            var expiredTemporary = alarms
                .Where(a => a.IsTemporary && a.DateTime < now)
                .ToList();

            // Assert
            expiredTemporary.Should().BeEmpty();
            alarms.Should().HaveCount(2);
        }

        #endregion

        #region CheckAlarms Logic Tests

        [Fact]
        public void CheckAlarms_TriggersAlarmAtExactMinute()
        {
            // Arrange
            var now = DateTime.Now;
            var currentMinute = TimeHelper.ToMinutePrecision(now);
            var alarm = new Alarm
            {
                Id = "test",
                Title = "Test Alarm",
                DateTime = currentMinute,
                IsEnabled = true,
                RepeatType = RepeatType.None
            };

            // Act
            var nextAlarmTime = alarm.GetNextAlarmTime();
            var shouldTrigger = nextAlarmTime.HasValue &&
                               TimeHelper.ToMinutePrecision(nextAlarmTime.Value) == currentMinute &&
                               (alarm.LastTriggered == null || alarm.LastTriggered.Value < currentMinute);

            // Assert
            shouldTrigger.Should().BeTrue();
        }

        [Fact]
        public void CheckAlarms_PreventsDuplicateTriggerInSameMinute()
        {
            // Arrange
            var now = DateTime.Now;
            var currentMinute = TimeHelper.ToMinutePrecision(now);
            var alarm = new Alarm
            {
                Id = "test",
                Title = "Test Alarm",
                DateTime = currentMinute,
                IsEnabled = true,
                RepeatType = RepeatType.None,
                LastTriggered = currentMinute // 이미 트리거됨
            };

            // Act
            var nextAlarmTime = alarm.GetNextAlarmTime();
            var shouldTrigger = nextAlarmTime.HasValue &&
                               TimeHelper.ToMinutePrecision(nextAlarmTime.Value) == currentMinute &&
                               (alarm.LastTriggered == null || alarm.LastTriggered.Value < currentMinute);

            // Assert
            shouldTrigger.Should().BeFalse();
        }

        #endregion
    }
}
