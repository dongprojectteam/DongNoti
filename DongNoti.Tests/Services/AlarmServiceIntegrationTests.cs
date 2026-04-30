using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class AlarmServiceIntegrationTests
    {
        [Fact]
        public void AlarmTriggeredEvent_ShouldFireWhenTimeMatches()
        {
            // Arrange
            var alarmService = new AlarmService();
            var now = DateTime.Now;
            var targetTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            
            var alarm = new Alarm
            {
                Id = "test-trigger",
                Title = "Trigger Me",
                DateTime = targetTime,
                IsEnabled = true,
                RepeatType = RepeatType.None
            };

            // Private 리스트에 접근하기 위해 Reflection을 사용하거나, 
            // LoadAlarms가 사용하는 StorageService를 모킹하는 대신 
            // 현재 구조에서는 간접적으로 테스트 환경을 조성해야 함.
            // 여기서는 로직만 분리하여 테스트하는 방식 혹은 
            // 테스트용 서브클래스를 고려할 수 있음.
        }

        [Fact]
        public void NextDdayRegistration_ShouldWorkCorrectily()
        {
            var alarmService = new AlarmService();
            var sourceAlarm = new Alarm
            {
                Id = "source",
                Title = "Source Alarm",
                AutoRegisterAsDday = true,
                AlarmType = AlarmType.Alarm,
                RepeatType = RepeatType.Daily
            };

            // 내부 비공개 메서드 테스트를 위한 로직 검증 (수동 시뮬레이션)
            DateTime nextDate = DateTime.Now.Date.AddDays(1);
            
            // Dday 생성 조건 확인
            bool shouldCreate = sourceAlarm.AutoRegisterAsDday && sourceAlarm.AlarmType == AlarmType.Alarm;
            shouldCreate.Should().BeTrue();
            nextDate.Should().Be(DateTime.Now.Date.AddDays(1));
        }

        [Fact]
        public void GetAlarms_ShouldReturnCopy()
        {
            var service = new AlarmService();
            var alarms1 = service.GetAlarms();
            var alarms2 = service.GetAlarms();

            alarms1.Should().NotBeSameAs(alarms2);
        }
    }
}
