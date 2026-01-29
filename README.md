# DongNoti

WPF(.NET 8) 기반의 트레이 알람 앱입니다.  
알람(일회/반복), Dday(카운트다운), 집중 모드(알람 억제 + 종료 후 놓친 알람 요약), 카테고리/우선순위, 통계, 단축키 안내 등을 제공합니다.

## 요구사항

- Windows 10/11
- .NET SDK: `net8.0-windows10.0.19041.0` (개발/빌드 시)
- Inno Setup 6 (설치 파일 생성 시)

## 실행/설치

- **개발 중 실행**: Visual Studio/`dotnet run`로 실행
- **배포(설치 파일 생성)**: `build-innosetup.bat` 실행 → `DongNoti_Setup.exe` 생성

## 빌드 (Inno Setup)

1) Inno Setup 설치 (ISCC 포함)
- 기본 경로: `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`

2) 설치 파일 생성

```bat
build-innosetup.bat
```

- 결과물: 프로젝트 루트에 `DongNoti_Setup.exe`
- 참고: Inno 스크립트는 `DongNoti.iss`

## 데이터 저장 위치

앱 데이터는 기본적으로 다음 경로에 저장됩니다:

- `%LOCALAPPDATA%\DongNoti\`
  - `alarms.json` : 알람 및 Dday 목록 (통합 저장)
  - `settings.json` : 앱 설정

## 주요 구성 요소(코드 구조)

- **UI**
  - `MainWindow` : 알람 목록/필터/집중모드/통계/단축키 (알람과 Dday 별도 DataGrid)
  - `Views/DdayWindow` : Dday 전용 창 (항상 최상단, 10초마다 실시간 업데이트)
  - `Views/*` : 설정/다이얼로그/팝업/요약/통계 등
  - 트레이 아이콘/메뉴: `App.xaml.cs` (`Hardcodet.NotifyIcon.Wpf`, 알람/Dday 분리)

- **Services**
  - `AlarmService` : 10초 주기 타이머로 알람 체크/트리거, 임시 알람 정리 (Alarm/Dday 통합 관리)
  - `FocusModeService` : 집중모드 상태/종료시간/프리셋/놓친 알람 기록
  - `NotificationService` : 알림 표시
  - `SoundService` : 사운드 재생
  - `StorageService` : `alarms.json`/`settings.json` 로드/저장(+ 내보내기/가져오기)
  - `LogService` : 파일/UI 로그

- **Models**
  - `Alarm` : 알람 및 Dday 통합 모델 (`AlarmType`, `TargetDate`, `Memo` 포함)
  - `AppSettings`, `FocusModePreset`, `MissedAlarm`, `AlarmHistory`, `Priority` 등

## 주요 기능

- **알람**: 일회/반복 알람 (매일/매주/매월), 사운드, 자동 종료, 카테고리/우선순위
- **Dday**: 카운트다운 기능 (D-30, D-1, D-day 형식), 메모, 별도 창 (항상 최상단), 트레이 메뉴 표시
- **집중 모드**: 알람 억제, 종료 후 놓친 알람 요약
- **통계**: 알람 히스토리, 카테고리별 통계
- **단축키**: Ctrl+N (추가), Del (삭제), Enter/F2 (편집), Ctrl+F (검색)

## 아키텍처 다이어그램(PlantUML)

`Architecture/` 디렉토리에 주요 다이어그램을 제공합니다.

- `Architecture/01-system-overview.puml` : 전체 컴포넌트 개요 (DdayWindow 포함)
- `Architecture/02-alarm-trigger-sequence.puml` : 알람 체크/트리거 시퀀스 (Dday는 알람 트리거 안 함)
- `Architecture/03-focus-mode-sequence.puml` : 집중모드 중 알람 억제/놓친 알람 기록/종료
- `Architecture/04-focus-mode-state.puml` : 집중모드 상태(State) 다이어그램
- `Architecture/05-data-files.puml` : 저장소(파일) 중심 구조 (Alarm 모델 필드 포함)

## 개발 메모

- 단일 인스턴스: `Mutex` + `NamedPipe`로 “이미 실행 중이면 기존 창 앞으로” 동작
- 알람 체크는 분 단위로 동작하도록 설계(초 단위 드리프트에 영향 최소화)

