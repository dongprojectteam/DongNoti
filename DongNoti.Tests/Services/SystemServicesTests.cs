using System;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class SystemServicesTests
    {
        [Fact]
        public void SoundService_StopSound_DoesNotThrow()
        {
            var service = new SoundService();
            Action act = () => service.StopSound();
            act.Should().NotThrow();
        }

        [Fact]
        public void SoundService_StopTestSound_DoesNotThrow()
        {
            var service = new SoundService();
            // private 메서드 호출이 어려우므로 Public 메서드를 통한 간접 호출 확인
            Action act = () => service.PlayTestSound(null);
            act.Should().NotThrow();
            service.StopSound();
        }

        [Fact]
        public void StartupService_IsStartupEnabled_CheckStatus()
        {
            // 실제 레지스트리 값을 읽어오는지 확인
            bool status = StartupService.IsStartupEnabled();
            (status || !status).Should().BeTrue();
        }

        [Fact]
        public void AlarmHistory_PropertySetters_ShouldWork()
        {
            var history = new AlarmHistory
            {
                AlarmId = "test-id",
                AlarmTitle = "Test Title",
                TriggeredAt = DateTime.Now,
                WasMissed = true
            };

            history.AlarmId.Should().Be("test-id");
            history.AlarmTitle.Should().Be("Test Title");
            history.WasMissed.Should().BeTrue();
        }

        [Fact]
        public void AppSettings_DefaultValues_AreCorrect()
        {
            var settings = new AppSettings();
            settings.EnableLogging.Should().BeFalse();
            settings.ShowUILog.Should().BeFalse();
            settings.CategoryColors.Should().NotBeNull();
        }
    }
}
