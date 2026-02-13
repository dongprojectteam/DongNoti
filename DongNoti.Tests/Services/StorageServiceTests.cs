using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class StorageServiceTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public StorageServiceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"DongNotiTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch { }
        }

        #region JSON Serialization/Deserialization Tests

        [Fact]
        public void Alarm_CanBeSerializedAndDeserialized()
        {
            var alarm = new Alarm
            {
                Id = "test-id",
                Title = "테스트 알람",
                DateTime = new DateTime(2024, 6, 15, 10, 30, 0),
                RepeatType = RepeatType.Daily,
                IsEnabled = true,
                Category = "업무",
                Priority = Priority.High,
                AutoDismissMinutes = 5,
                Memo = "메모 내용"
            };
            var json = JsonSerializer.Serialize(alarm, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<Alarm>(json, _jsonOptions);
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be("test-id");
            deserialized.Title.Should().Be("테스트 알람");
            deserialized.DateTime.Should().Be(new DateTime(2024, 6, 15, 10, 30, 0));
            deserialized.RepeatType.Should().Be(RepeatType.Daily);
            deserialized.IsEnabled.Should().BeTrue();
            deserialized.Category.Should().Be("업무");
            deserialized.Priority.Should().Be(Priority.High);
            deserialized.AutoDismissMinutes.Should().Be(5);
            deserialized.Memo.Should().Be("메모 내용");
        }

        [Fact]
        public void AlarmList_CanBeSerializedAndDeserialized()
        {
            var alarms = new List<Alarm>
            {
                new Alarm { Title = "알람 1", RepeatType = RepeatType.None },
                new Alarm { Title = "알람 2", RepeatType = RepeatType.Daily },
                new Alarm { Title = "알람 3", RepeatType = RepeatType.Weekly, SelectedDaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday } }
            };
            var json = JsonSerializer.Serialize(alarms, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<List<Alarm>>(json, _jsonOptions);
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(3);
            deserialized![0].Title.Should().Be("알람 1");
            deserialized[1].RepeatType.Should().Be(RepeatType.Daily);
            deserialized[2].SelectedDaysOfWeek.Should().Contain(DayOfWeek.Monday);
            deserialized[2].SelectedDaysOfWeek.Should().Contain(DayOfWeek.Friday);
        }

        [Fact]
        public void AppSettings_CanBeSerializedAndDeserialized()
        {
            var settings = new AppSettings
            {
                RunOnStartup = false,
                HideToTrayOnStartup = true,
                MinimizeToTray = true,
                EnableLogging = true,
                FocusModeActive = true,
                FocusModeEndTime = new DateTime(2024, 6, 15, 12, 0, 0),
                DefaultFocusModePresetId = "1h",
                DdayWindowVisible = true
            };
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            deserialized.Should().NotBeNull();
            deserialized!.RunOnStartup.Should().BeFalse();
            deserialized.HideToTrayOnStartup.Should().BeTrue();
            deserialized.EnableLogging.Should().BeTrue();
            deserialized.FocusModeActive.Should().BeTrue();
            deserialized.FocusModeEndTime.Should().Be(new DateTime(2024, 6, 15, 12, 0, 0));
            deserialized.DefaultFocusModePresetId.Should().Be("1h");
            deserialized.DdayWindowVisible.Should().BeTrue();
        }

        [Fact]
        public void DdayAlarm_CanBeSerializedAndDeserialized()
        {
            var dday = new Alarm
            {
                Title = "기념일",
                AlarmType = AlarmType.Dday,
                TargetDate = new DateTime(2024, 12, 25),
                Memo = "크리스마스"
            };
            var json = JsonSerializer.Serialize(dday, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<Alarm>(json, _jsonOptions);
            deserialized.Should().NotBeNull();
            deserialized!.AlarmType.Should().Be(AlarmType.Dday);
            deserialized.TargetDate.Should().Be(new DateTime(2024, 12, 25));
            deserialized.Memo.Should().Be("크리스마스");
        }

        #endregion

        #region File I/O Tests

        [Fact]
        public void ExportAlarms_WritesJsonToFile()
        {
            var alarms = new List<Alarm>
            {
                new Alarm { Title = "TestAlarm1" },
                new Alarm { Title = "TestAlarm2" }
            };
            var filePath = Path.Combine(_testDirectory, "export_test.json");
            var result = StorageService.ExportAlarms(alarms, filePath);
            result.Should().BeTrue();
            File.Exists(filePath).Should().BeTrue();
            
            var content = File.ReadAllText(filePath);
            content.Should().Contain("TestAlarm1");
            content.Should().Contain("TestAlarm2");
        }

        [Fact]
        public void ImportAlarms_ReadsJsonFromFile()
        {
            var alarms = new List<Alarm>
            {
                new Alarm { Title = "ImportTest" }
            };
            var filePath = Path.Combine(_testDirectory, "import_test.json");
            var json = JsonSerializer.Serialize(alarms, _jsonOptions);
            File.WriteAllText(filePath, json);
            var result = StorageService.ImportAlarms(filePath);
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result![0].Title.Should().Be("ImportTest");
        }

        #endregion

        #region GetDataDirectory

        [Fact]
        public void GetDataDirectory_ReturnsNonEmptyPath()
        {
            var result = StorageService.GetDataDirectory();
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("DongNoti");
        }

        #endregion

        #region Interface Implementation

        [Fact]
        public void Instance_ImplementsIStorageService()
        {
            StorageService.Instance.Should().BeAssignableTo<IStorageService>();
        }

        [Fact]
        public void IStorageService_LoadAlarms_ReturnsNonNull()
        {
            IStorageService service = StorageService.Instance;
            var result = service.LoadAlarms();
            result.Should().NotBeNull();
        }

        [Fact]
        public void IStorageService_LoadSettings_ReturnsNonNull()
        {
            IStorageService service = StorageService.Instance;
            var result = service.LoadSettings();
            result.Should().NotBeNull();
        }

        [Fact]
        public void IStorageService_GetDataDirectory_ReturnsNonEmpty()
        {
            IStorageService service = StorageService.Instance;
            var result = service.GetDataDirectory();
            result.Should().NotBeNullOrEmpty();
        }

        #endregion
    }
}
