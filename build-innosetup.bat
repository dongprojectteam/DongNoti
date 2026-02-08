@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

echo ========================================
echo DongNoti Inno Setup Build
echo ========================================
echo.
echo This script automatically performs the following:
echo   1. Publish in Release mode (self-contained)
echo   2. Create installer using Inno Setup
echo   3. Clean temporary publish folder
echo.
pause
echo.

REM Save current directory
set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

REM Output directory settings
set "PUBLISH_DIR=publish_inno"
set "ISS_FILE=DongNoti.iss"
set "INNO_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

echo ========================================
echo [1/4] Cleaning previous build files...
echo ========================================
echo.
if exist "%PUBLISH_DIR%" (
    echo Removing: %PUBLISH_DIR%\
    rmdir /s /q "%PUBLISH_DIR%"
)
echo Done.
echo.

echo ========================================
echo [2/4] Publishing in Release mode...
echo ========================================
echo.
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%PUBLISH_DIR%"
if errorlevel 1 (
    echo.
    echo Error: dotnet publish failed!
    pause
    exit /b 1
)
echo Done.
echo.

echo ========================================
echo [3/4] Compiling Inno Setup script...
echo ========================================
echo.

if not exist "%INNO_PATH%" (
    echo Error: Inno Setup compiler ISCC.exe not found!
    echo Path: "!INNO_PATH!"
    echo.
    echo Please make sure Inno Setup is installed at:
    echo   - "C:\Program Files (x86)\Inno Setup 6\"
    echo.
    pause
    exit /b 1
)

if not exist "%ISS_FILE%" (
    echo Error: ISS file not found!
    echo File: %ISS_FILE%
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
    echo Error: Inno Setup compilation failed!
    pause
    exit /b 1
)
echo Done.
echo.

echo ========================================
echo [4/4] Cleaning temporary publish folder...
echo ========================================
echo.
if exist "%PUBLISH_DIR%" (
    echo Removing: %PUBLISH_DIR%\
    rmdir /s /q "%PUBLISH_DIR%"
)
echo Done.
echo.

echo ========================================
echo Build complete!
echo ========================================
echo.
if exist "DongNoti_Setup.exe" (
    echo Generated setup file: DongNoti_Setup.exe
) else (
    echo Warning: DongNoti_Setup.exe not found. Check Inno Setup output settings.
)
echo.
pause

