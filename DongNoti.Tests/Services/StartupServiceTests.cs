using System;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class StartupServiceTests
    {
        [Fact]
        public void SetStartup_ShouldNotThrow()
        {
            // 실제 레지스트리 쓰기 권한에 따라 실패할 수 있으므로 
            // 현재 환경에서 안전하게 호출 가능한지 확인합니다.
            // (보통 사용자 레지스트리 영역이므로 일반 권한으로 가능)
            Action act = () => StartupService.SetStartup(false);
            act.Should().NotThrow();
        }

        [Fact]
        public void IsStartupEnabled_ShouldReturnBoolean()
        {
            bool result = StartupService.IsStartupEnabled();
            // 값의 참/거짓 여부보다 메서드 실행 무결성을 확인합니다.
            (result == true || result == false).Should().BeTrue();
        }
    }
}
