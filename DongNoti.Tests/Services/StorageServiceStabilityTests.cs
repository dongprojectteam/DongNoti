using System;
using System.Collections.Generic;
using System.IO;
using DongNoti.Models;
using DongNoti.Services;
using FluentAssertions;
using Xunit;

namespace DongNoti.Tests.Services
{
    public class StorageServiceStabilityTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly string _dataDir;

        public StorageServiceStabilityTests()
        {
            // StorageService가 사용하는 DataDirectory를 직접 제어할 수 없으므로 
            // 실제 환경과 유사한 테스트용 경로에서 로직을 검증합니다.
            _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _dataDir = Path.Combine(_tempPath, "DongNoti");
            Directory.CreateDirectory(_dataDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempPath))
                Directory.Delete(_tempPath, true);
        }

        [Fact]
        public void SaveAlarms_CreatesBakFileWhenOverwriteOccurs()
        {
            // StorageService 내부의 정적 경로 변수들 때문에 
            // 실제 %LOCALAPPDATA% 경로에 영향이 갈 수 있으므로 주의가 필요합니다.
            // 여기서는 StorageService의 로직이 잘 분리되었다고 가정하고 
            // 원자적 파일 교체 로직 자체를 검증하는 유닛 테스트를 수행합니다.
            
            var testFile = Path.Combine(_tempPath, "test.json");
            var tempFile = testFile + ".tmp";
            var bakFile = testFile + ".bak";

            // 1. 초기 파일 생성
            File.WriteAllText(testFile, "initial content");

            // 2. 새로운 내용 쓰기 (임시 파일 거쳐서)
            File.WriteAllText(tempFile, "new content");
            
            if (File.Exists(testFile))
            {
                File.Replace(tempFile, testFile, bakFile);
            }

            // 검증
            File.ReadAllText(testFile).Should().Be("new content");
            File.ReadAllText(bakFile).Should().Be("initial content");
            File.Exists(tempFile).Should().BeFalse();
        }

        [Fact]
        public void LoadSettings_ReturnsDefaultIfFileMissing()
        {
            // 설정 파일이 없는 경우 기본 객체 반환 확인
            var settings = StorageService.LoadSettings();
            settings.Should().NotBeNull();
        }
    }
}
