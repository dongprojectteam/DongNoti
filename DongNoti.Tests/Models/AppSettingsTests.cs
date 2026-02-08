using System.Collections.Generic;
using DongNoti.Models;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Models
{
    public class AppSettingsTests
    {
        #region Default Values

        [Fact]
        public void AppSettings_DefaultValues_AreCorrect()
        {
            // Act
            var settings = new AppSettings();

            // Assert
            settings.RunOnStartup.Should().BeTrue();
            settings.HideToTrayOnStartup.Should().BeFalse();
            settings.MinimizeToTray.Should().BeTrue();
            settings.EnableLogging.Should().BeFalse();
            settings.ShowUILog.Should().BeFalse();
            settings.FocusModeActive.Should().BeFalse();
            settings.FocusModeEndTime.Should().BeNull();
            settings.DefaultFocusModePresetId.Should().Be("30m");
            settings.DdayWindowVisible.Should().BeFalse();
        }

        [Fact]
        public void AppSettings_FocusModePresets_IsNotNull()
        {
            // Act
            var settings = new AppSettings();

            // Assert
            settings.FocusModePresets.Should().NotBeNull();
        }

        [Fact]
        public void AppSettings_CurrentMissedAlarms_IsNotNull()
        {
            // Act
            var settings = new AppSettings();

            // Assert
            settings.CurrentMissedAlarms.Should().NotBeNull();
        }

        [Fact]
        public void AppSettings_AlarmHistory_IsNotNull()
        {
            // Act
            var settings = new AppSettings();

            // Assert
            settings.AlarmHistory.Should().NotBeNull();
        }

        #endregion

        #region GetDefaultPresets

        [Fact]
        public void GetDefaultPresets_ReturnsSixPresets()
        {
            // Act
            var presets = AppSettings.GetDefaultPresets();

            // Assert
            presets.Should().HaveCount(6);
        }

        [Fact]
        public void GetDefaultPresets_ContainsExpectedPresets()
        {
            // Act
            var presets = AppSettings.GetDefaultPresets();

            // Assert
            presets.Should().Contain(p => p.Id == "30m" && p.Minutes == 30);
            presets.Should().Contain(p => p.Id == "1h" && p.Minutes == 60);
            presets.Should().Contain(p => p.Id == "1h30m" && p.Minutes == 90);
            presets.Should().Contain(p => p.Id == "2h" && p.Minutes == 120);
            presets.Should().Contain(p => p.Id == "2h30m" && p.Minutes == 150);
            presets.Should().Contain(p => p.Id == "3h" && p.Minutes == 180);
        }

        [Fact]
        public void GetDefaultPresets_PresetsHaveKoreanNames()
        {
            // Act
            var presets = AppSettings.GetDefaultPresets();

            // Assert
            presets.Should().Contain(p => p.DisplayName == "30분");
            presets.Should().Contain(p => p.DisplayName == "1시간");
            presets.Should().Contain(p => p.DisplayName == "1시간 30분");
            presets.Should().Contain(p => p.DisplayName == "2시간");
            presets.Should().Contain(p => p.DisplayName == "2시간 30분");
            presets.Should().Contain(p => p.DisplayName == "3시간");
        }

        #endregion

        #region GetDefaultAlarmCategories

        [Fact]
        public void GetDefaultAlarmCategories_ReturnsFiveCategories()
        {
            // Act
            var categories = AppSettings.GetDefaultAlarmCategories();

            // Assert
            categories.Should().HaveCount(5);
        }

        [Fact]
        public void GetDefaultAlarmCategories_ContainsExpectedCategories()
        {
            // Act
            var categories = AppSettings.GetDefaultAlarmCategories();

            // Assert
            categories.Should().Contain("기본");
            categories.Should().Contain("업무");
            categories.Should().Contain("개인");
            categories.Should().Contain("약속");
            categories.Should().Contain("기념일");
        }

        [Fact]
        public void GetDefaultAlarmCategories_DefaultIsFirst()
        {
            // Act
            var categories = AppSettings.GetDefaultAlarmCategories();

            // Assert
            categories[0].Should().Be("기본");
        }

        #endregion

        #region AlarmCategories Default

        [Fact]
        public void AlarmCategories_DefaultValue_MatchesGetDefaultAlarmCategories()
        {
            // Act
            var settings = new AppSettings();

            // Assert
            settings.AlarmCategories.Should().BeEquivalentTo(AppSettings.GetDefaultAlarmCategories());
        }

        #endregion

        #region CategoryColors Default

        [Fact]
        public void CategoryColors_DefaultValue_ContainsAnniversaryColor()
        {
            // Act
            var settings = new AppSettings();

            // Assert
            settings.CategoryColors.Should().ContainKey("기념일");
            settings.CategoryColors["기념일"].Should().Be("#E91E63");
        }

        #endregion
    }
}
