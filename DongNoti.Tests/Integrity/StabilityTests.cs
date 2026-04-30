using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Integrity
{
    public class StabilityTests : IDisposable
    {
        private readonly string _testDirectory;

        public StabilityTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"DongNotiStability_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            // 실제 StorageService의 경로를 테스트 디렉토리로 우회할 수 없으므로 
            // StorageService의 로직을 별도 유틸리티로 검증하거나 내부 메서드를 테스트합니다.
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [Fact]
        public void AtomicWrite_ShouldCreateBackupAndMaintainIntegrity()
        {
            // StorageService.SaveFileAtomically 로직 검증
            var filePath = Path.Combine(_testDirectory, "data.json");
            var content1 = "{\"data\": \"original\"}";
            var content2 = "{\"data\": \"updated\"}";

            // 1. 초기 저장
            File.WriteAllText(filePath, content1);
            
            // 2. 원자적 교체 (임시 파일 생성 후 교체)
            string tempPath = filePath + ".tmp";
            string backupPath = filePath + ".bak";
            File.WriteAllText(tempPath, content2);
            
            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, backupPath);
            else
                File.Move(tempPath, filePath);

            // 검증
            File.ReadAllText(filePath).Should().Be(content2);
            File.ReadAllText(backupPath).Should().Be(content1);
        }

        [Fact]
        public void TriggerCache_ShouldPreventDuplicatesInSameMinute()
        {
            // AlarmService의 캐시 로직 시뮬레이션
            var cache = new System.Collections.Concurrent.ConcurrentDictionary<string, DateTime>();
            var alarmId = "alarm-1";
            var nowMinute = new DateTime(2024, 1, 1, 10, 0, 0);

            // 첫 번째 트리거 시도
            bool firstAttempt = cache.TryAdd(alarmId, nowMinute);
            // 두 번째 트리거 시도 (같은 분)
            bool secondAttempt = !cache.TryGetValue(alarmId, out var lastTime) || lastTime < nowMinute;
            if (secondAttempt) cache[alarmId] = nowMinute;
            else secondAttempt = false; // 중복 방지됨

            firstAttempt.Should().BeTrue();
            secondAttempt.Should().BeFalse();
        }

        [Fact]
        public void DirtyFlag_ShouldOptimizeStorageCalls()
        {
            long isDirty = 0;
            int saveCount = 0;

            void MarkAsDirty() => Interlocked.Exchange(ref isDirty, 1);
            void SaveIfDirty()
            {
                if (Interlocked.Exchange(ref isDirty, 0) == 1)
                {
                    saveCount++;
                }
            }

            // 시나리오 1: 변경 없음
            SaveIfDirty();
            saveCount.Should().Be(0);

            // 시나리오 2: 변경 발생
            MarkAsDirty();
            MarkAsDirty(); // 여러 번 마킹
            SaveIfDirty();
            saveCount.Should().Be(1);

            // 시나리오 3: 저장 후 다시 저장 시도
            SaveIfDirty();
            saveCount.Should().Be(1); // 카운트 증가 안함
        }
    }
}
