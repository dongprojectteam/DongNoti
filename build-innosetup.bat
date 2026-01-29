@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

echo ========================================
echo DongNoti Inno Setup 빌드
echo ========================================
echo.
echo 이 스크립트는 다음을 자동으로 수행합니다:
echo   1. Release 모드로 publish (self-contained)
echo   2. Inno Setup으로 설치 파일 생성
echo   3. 임시 publish 폴더 정리
echo.
pause
echo.

REM 현재 디렉토리 저장
set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

REM 출력 디렉토리 설정
set "PUBLISH_DIR=publish_inno"
set "ISS_FILE=DongNoti.iss"
set "INNO_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

echo ========================================
echo [1/4] 기존 빌드 파일 정리...
echo ========================================
echo.
if exist "%PUBLISH_DIR%" (
    echo 정리 중: %PUBLISH_DIR%\
    rmdir /s /q "%PUBLISH_DIR%"
)
echo 완료.
echo.

echo ========================================
echo [2/4] Release 모드로 publish...
echo ========================================
echo.
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%PUBLISH_DIR%"
if errorlevel 1 (
    echo.
    echo 오류: dotnet publish 실패!
    pause
    exit /b 1
)
echo 완료.
echo.

echo ========================================
echo [3/4] Inno Setup 컴파일...
echo ========================================
echo.

if not exist "%INNO_PATH%" (
    echo 오류: Inno Setup 컴파일러 파일 ISCC.exe 를 찾을 수 없습니다!
    echo 경로: "!INNO_PATH!"
    echo.
    echo Inno Setup이 다음 경로에 설치되어 있는지 확인하세요:
    echo   - "C:\Program Files (x86)\Inno Setup 6\"
    echo.
    pause
    exit /b 1
)

if not exist "%ISS_FILE%" (
    echo 오류: ISS 파일을 찾을 수 없습니다!
    echo 파일: %ISS_FILE%
    echo.
    pause
    exit /b 1
)

echo ISCC: %INNO_PATH%
echo ISS : %ISS_FILE%
echo.

"%INNO_PATH%" "%ISS_FILE%"
if errorlevel 1 (
    echo.
    echo 오류: Inno Setup 컴파일 실패!
    pause
    exit /b 1
)
echo 완료.
echo.

echo ========================================
echo [4/4] 임시 publish 폴더 정리...
echo ========================================
echo.
if exist "%PUBLISH_DIR%" (
    echo 정리 중: %PUBLISH_DIR%\
    rmdir /s /q "%PUBLISH_DIR%"
)
echo 완료.
echo.

echo ========================================
echo 빌드 완료!
echo ========================================
echo.
if exist "DongNoti_Setup.exe" (
    echo 생성된 설치 파일: DongNoti_Setup.exe
) else (
    echo 경고: DongNoti_Setup.exe가 보이지 않습니다. Inno Setup 출력 설정을 확인하세요.
)
echo.
pause

