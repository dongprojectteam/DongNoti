# DongNoti.Tests

DongNoti 프로젝트의 Unit Test 프로젝트입니다.

## 테스트 프레임워크

- **xUnit** - 테스트 프레임워크
- **FluentAssertions** - 가독성 높은 assertion
- **Moq** - Mocking 라이브러리
- **coverlet** - Code Coverage 측정

## 테스트 실행 방법

### 기본 실행

```powershell
# 프로젝트 루트에서 실행
dotnet test DongNoti.Tests\DongNoti.Tests.csproj
```

### 상세 출력

```powershell
dotnet test DongNoti.Tests\DongNoti.Tests.csproj --verbosity normal
```

### Coverage 측정

```powershell
dotnet test DongNoti.Tests\DongNoti.Tests.csproj --collect:"XPlat Code Coverage" --results-directory .\TestResults
```

Coverage 결과는 `TestResults` 폴더에 `coverage.cobertura.xml` 파일로 생성됩니다.

### PowerShell 스크립트 사용

프로젝트 루트의 `run-tests.ps1` 스크립트를 사용할 수 있습니다:

```powershell
# 기본 실행 (Coverage 포함)
.\run-tests.ps1

# HTML 리포트 생성 (ReportGenerator 필요)
.\run-tests.ps1 -Html

# 상세 출력
.\run-tests.ps1 -Verbose
```

### HTML Coverage 리포트 생성

HTML 리포트를 생성하려면 ReportGenerator를 설치해야 합니다:

```powershell
# ReportGenerator 설치
dotnet tool install -g dotnet-reportgenerator-globaltool

# HTML 리포트 생성
.\run-tests.ps1 -Html
```

리포트는 `CoverageReport\index.html`에 생성됩니다.

## 테스트 구조

```
DongNoti.Tests/
├── Services/
│   ├── TimeHelperTests.cs      # TimeHelper 유틸리티 테스트
│   └── StorageServiceTests.cs  # StorageService 테스트
├── Models/
│   ├── AlarmTests.cs           # Alarm 모델 테스트
│   └── AppSettingsTests.cs     # AppSettings 테스트
└── Converters/
    └── ConverterTests.cs       # WPF Converter 테스트
```

## 테스트 대상

| 클래스 | 테스트 내용 |
|--------|-------------|
| `TimeHelper` | `FormatTimeSpan`, `ToMinutePrecision` |
| `Alarm` | `GetNextAlarmTime`, `DaysRemaining`, `DdayDisplayString`, 기본값 검증 |
| `AppSettings` | `GetDefaultPresets`, `GetDefaultAlarmCategories`, 기본값 검증 |
| `NullToBoolConverter` | null/non-null 변환 |
| `AlarmTypeToVisibilityConverter` | AlarmType별 Visibility 변환 |
| `StorageService` | JSON 직렬화/역직렬화, 파일 I/O |
| `StatisticsService` | 통계 계산 로직, 날짜 필터링, 그룹화 |
| `FocusModeService` | 남은 시간 계산, 프리셋 로직, 중복 방지 |
| `AlarmService` | 히스토리 기록, 임시 알람 정리, 트리거 로직 |

## Coverage

현재 Coverage는 약 **26%** 수준입니다. UI 코드(WPF Window, Dialog 등)는 테스트에서 제외되며, 비즈니스 로직 위주로 측정됩니다.

Coverage를 더 높이려면:
- 서비스 클래스의 DI 패턴 도입
- UI 로직과 비즈니스 로직 분리
- 추가 서비스 테스트 작성

## 인터페이스

테스트 가능성을 위해 다음 인터페이스가 추가되었습니다:

- `IStorageService` - 스토리지 작업 추상화
- `ILogService` - 로깅 작업 추상화

Mocking이 필요한 경우 이 인터페이스를 사용하세요:

```csharp
var mockStorage = new Mock<IStorageService>();
mockStorage.Setup(s => s.LoadAlarms()).Returns(new List<Alarm>());
```

## 주의사항

- `ExportAlarms`/`ImportAlarms` 메서드는 `MessageBox`를 사용하므로 일부 테스트가 제한됩니다.
- WPF UI 관련 코드는 직접 테스트하기 어려우므로 비즈니스 로직 위주로 테스트합니다.
