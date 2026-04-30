using System;
using System.IO;
using System.Linq;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class LogServiceTests : IDisposable
    {
        public LogServiceTests()
        {
            LogService.Initialize();
        }

        public void Dispose()
        {
        }

        [Fact]
        public void Log_ShouldAppendToBuffer()
        {
            LogService.SetEnabled(true);
            int initialCount = LogService.BufferedLogCount;
            var message = $"Test Info Message {Guid.NewGuid()}";

            LogService.LogInfo(message);

            LogService.BufferedLogCount.Should().BeGreaterThanOrEqualTo(initialCount + 1);
            var logs = LogService.GetBufferedLogs();
            logs.Should().Contain(log => log.Contains(message));
        }

        [Fact]
        public void SetEnabled_ShouldToggleLogging()
        {
            LogService.SetEnabled(false);
            LogService.IsEnabled.Should().BeFalse();

            LogService.SetEnabled(true);
            LogService.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void GetCurrentLogFilePath_ShouldReturnValidPath()
        {
            var path = LogService.GetCurrentLogFilePath();
            path.Should().NotBeNullOrEmpty();
            path.Should().Contain("Logs");
        }

        [Fact]
        public void Flush_ShouldNotThrow()
        {
            LogService.LogInfo("Flush Test");
            Action act = () => LogService.Flush();
            act.Should().NotThrow();
        }
    }
}
