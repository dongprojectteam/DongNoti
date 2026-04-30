using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class AlarmServiceTests : IDisposable
    {
        private readonly Mock<IStorageService> _mockStorage;
        
        public AlarmServiceTests()
        {
            FocusModeService.Instance.StopFocusMode();
            FocusModeService.Instance.ClearMissedAlarms();
            _mockStorage = new Mock<IStorageService>();
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm>());
            _mockStorage.Setup(s => s.LoadSettings()).Returns(new AppSettings());
        }

        public void Dispose()
        {
            // 필요한 경우 정리 로직
        }

        [Fact]
        public void Constructor_ShouldLoadAlarmsFromStorage()
        {
            var alarms = new List<Alarm> { new Alarm { Title = "Test" } };
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(alarms);

            var service = new AlarmService(_mockStorage.Object);

            service.GetAlarms().Should().HaveCount(1);
            _mockStorage.Verify(s => s.LoadAlarms(), Times.Once);
        }

        [Fact]
        public void CheckAlarms_ShouldTriggerEvent_WhenTimeMatches()
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            var alarm = new Alarm 
            { 
                Id = "a1", 
                Title = "Match", 
                DateTime = target, 
                IsEnabled = true,
                RepeatType = RepeatType.None 
            };
            
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm> { alarm });
            var service = new AlarmService(_mockStorage.Object);
            
            bool eventFired = false;
            service.AlarmTriggered += (a) => { if (a.Id == "a1") eventFired = true; };

            // 내부 비공개 메서드 CheckAlarms를 수동 호출 (타이머 대신)
            var checkMethod = typeof(AlarmService).GetMethod("CheckAlarms", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            checkMethod!.Invoke(service, new object[] { null!, null! });

            eventFired.Should().BeTrue();
        }

        [Fact]
        public void CheckAlarms_ShouldNotTriggerDuplicateInSameMinute()
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            var alarm = new Alarm 
            { 
                Id = "a1", 
                DateTime = target, 
                IsEnabled = true 
            };
            
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm> { alarm });
            var service = new AlarmService(_mockStorage.Object);
            
            int fireCount = 0;
            service.AlarmTriggered += (a) => fireCount++;

            var checkMethod = typeof(AlarmService).GetMethod("CheckAlarms", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            checkMethod!.Invoke(service, new object[] { null!, null! }); // 1차
            checkMethod!.Invoke(service, new object[] { null!, null! }); // 2차 (중복)

            fireCount.Should().Be(1);
        }

        [Fact]
        public void CleanupExpiredTemporaryAlarms_ShouldRemovePastAlarms()
        {
            var pastTemp = new Alarm { Title = "Past", DateTime = DateTime.Now.AddHours(-1), IsTemporary = true };
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm> { pastTemp });
            
            var service = new AlarmService(_mockStorage.Object);
            // 생성자에서 Cleanup이 한 번 호출됨

            service.GetAlarms().Should().BeEmpty();
        }

        [Fact]
        public void RecordAlarmHistory_ShouldCallSaveSettings()
        {
            var alarm = new Alarm { Id = "h1", Title = "History Test" };
            var settings = new AppSettings();
            _mockStorage.Setup(s => s.LoadSettings()).Returns(settings);

            var service = new AlarmService(_mockStorage.Object);
            service.RecordAlarmHistory(alarm, false);

            _mockStorage.Verify(s => s.SaveSettings(It.IsAny<AppSettings>()), Times.AtLeastOnce);
            settings.AlarmHistory.Should().Contain(h => h.AlarmId == "h1");
        }

        [Fact]
        public void CreateNextDdayForAlarm_ShouldAddDdayToList()
        {
            var alarm = new Alarm 
            { 
                Id = "a1", 
                Title = "Source", 
                AlarmType = AlarmType.Alarm, 
                AutoRegisterAsDday = true,
                RepeatType = RepeatType.Daily
            };
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm> { alarm });

            var service = new AlarmService(_mockStorage.Object);
            service.CreateNextDdayForAlarm("a1");

            var alarms = service.GetAlarms();
            alarms.Should().Contain(a => a.AlarmType == AlarmType.Dday && a.Title.Contains("Source"));
        }

        [Fact]
        public void GetAlarms_ShouldReturnDeepCopies()
        {
            var alarm = new Alarm
            {
                Id = "copy-1",
                Title = "Original",
                SelectedDaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday }
            };
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm> { alarm });

            var service = new AlarmService(_mockStorage.Object);
            var firstSnapshot = service.GetAlarms();
            firstSnapshot[0].Title = "Mutated";
            firstSnapshot[0].SelectedDaysOfWeek.Add(DayOfWeek.Friday);

            var secondSnapshot = service.GetAlarms();

            secondSnapshot[0].Title.Should().Be("Original");
            secondSnapshot[0].SelectedDaysOfWeek.Should().Equal(DayOfWeek.Monday);
        }

        [Fact]
        public void AddAlarm_ShouldPersistAndRaiseChangedEvent()
        {
            var service = new AlarmService(_mockStorage.Object);
            IReadOnlyList<Alarm>? changedSnapshot = null;
            service.AlarmsChanged += alarms => changedSnapshot = alarms;

            service.AddAlarm(new Alarm { Id = "new-1", Title = "New alarm" });

            service.GetAlarms().Should().ContainSingle(a => a.Id == "new-1");
            changedSnapshot.Should().NotBeNull();
            changedSnapshot!.Should().ContainSingle(a => a.Id == "new-1");
            _mockStorage.Verify(s => s.SaveAlarms(It.Is<List<Alarm>>(alarms =>
                alarms.Count == 1 && alarms[0].Id == "new-1")), Times.Once);
        }

        [Fact]
        public void UpdateAlarm_ShouldReplaceExistingAlarmOnly()
        {
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm>
            {
                new Alarm { Id = "a1", Title = "Old" },
                new Alarm { Id = "a2", Title = "Keep" }
            });
            var service = new AlarmService(_mockStorage.Object);

            var updated = service.UpdateAlarm(new Alarm { Id = "a1", Title = "Updated" });
            var missing = service.UpdateAlarm(new Alarm { Id = "missing", Title = "Nope" });

            updated.Should().BeTrue();
            missing.Should().BeFalse();
            service.GetAlarms().Should().Contain(a => a.Id == "a1" && a.Title == "Updated");
            service.GetAlarms().Should().Contain(a => a.Id == "a2" && a.Title == "Keep");
        }

        [Fact]
        public void DeleteAlarm_ShouldRemoveExistingAlarm()
        {
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm>
            {
                new Alarm { Id = "delete-me" },
                new Alarm { Id = "keep-me" }
            });
            var service = new AlarmService(_mockStorage.Object);

            service.DeleteAlarm("delete-me").Should().BeTrue();
            service.DeleteAlarm("missing").Should().BeFalse();
            service.DeleteAlarm("").Should().BeFalse();

            service.GetAlarms().Should().ContainSingle(a => a.Id == "keep-me");
        }

        [Fact]
        public void ReplaceAlarms_ShouldPersistSnapshotAndCloneInput()
        {
            var service = new AlarmService(_mockStorage.Object);
            var replacement = new List<Alarm>
            {
                new Alarm { Id = "r1", Title = "Replacement" }
            };

            var count = service.ReplaceAlarms(replacement);
            replacement[0].Title = "Mutated outside";

            count.Should().Be(1);
            service.GetAlarms().Should().ContainSingle(a => a.Title == "Replacement");
        }

        [Fact]
        public void RemoveAlarms_ShouldRemoveMatchingItems()
        {
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm>
            {
                new Alarm { Id = "alarm", AlarmType = AlarmType.Alarm },
                new Alarm { Id = "dday", AlarmType = AlarmType.Dday }
            });
            var service = new AlarmService(_mockStorage.Object);

            var removed = service.RemoveAlarms(a => a.AlarmType == AlarmType.Dday);
            var removedAgain = service.RemoveAlarms(a => a.AlarmType == AlarmType.Dday);

            removed.Should().Be(1);
            removedAgain.Should().Be(0);
            service.GetAlarms().Should().ContainSingle(a => a.Id == "alarm");
        }

        [Fact]
        public void UpdateAlarms_ShouldApplyBulkUpdateOnlyToMatches()
        {
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm>
            {
                new Alarm { Id = "alarm", AlarmType = AlarmType.Alarm, IsEnabled = false },
                new Alarm { Id = "dday", AlarmType = AlarmType.Dday, IsEnabled = false }
            });
            var service = new AlarmService(_mockStorage.Object);

            var updated = service.UpdateAlarms(
                a => a.AlarmType == AlarmType.Alarm,
                a => a.IsEnabled = true);
            var updatedAgain = service.UpdateAlarms(
                a => a.Id == "missing",
                a => a.IsEnabled = true);

            updated.Should().Be(1);
            updatedAgain.Should().Be(0);
            service.GetAlarms().Single(a => a.Id == "alarm").IsEnabled.Should().BeTrue();
            service.GetAlarms().Single(a => a.Id == "dday").IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void Setters_ShouldUpdateSpecificAlarmFields()
        {
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm>
            {
                new Alarm { Id = "a1", IsEnabled = true, AutoRegisterAsDday = false }
            });
            var service = new AlarmService(_mockStorage.Object);
            var triggeredAt = new DateTime(2026, 4, 28, 8, 30, 0);

            service.SetAlarmEnabled("a1", false).Should().BeTrue();
            service.SetAutoRegisterAsDday("a1", true).Should().BeTrue();
            service.SetLastTriggered("a1", triggeredAt).Should().BeTrue();
            service.SetAlarmEnabled("missing", true).Should().BeFalse();

            var alarm = service.GetAlarms().Single();
            alarm.IsEnabled.Should().BeFalse();
            alarm.AutoRegisterAsDday.Should().BeTrue();
            alarm.LastTriggered.Should().Be(triggeredAt);
        }

        [Fact]
        public void SnoozeAlarm_ShouldUpdateOriginalAndAddTemporaryAlarm()
        {
            _mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm>
            {
                new Alarm { Id = "original", Title = "Original" }
            });
            var service = new AlarmService(_mockStorage.Object);
            var triggeredAt = new DateTime(2026, 4, 28, 8, 0, 0);
            var snoozeAlarm = new Alarm
            {
                Id = "snooze",
                Title = "Snooze",
                IsTemporary = true
            };

            service.SnoozeAlarm("original", triggeredAt, snoozeAlarm);

            var alarms = service.GetAlarms();
            alarms.Single(a => a.Id == "original").LastTriggered.Should().Be(triggeredAt);
            alarms.Single(a => a.Id == "snooze").IsTemporary.Should().BeTrue();
        }
    }
}
