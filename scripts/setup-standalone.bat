@echo off
REM QuickView Standalone Setup
REM Compiles and runs QuickView from current directory (no installation required)

echo ========================================
echo QuickView Standalone Setup
echo ========================================
echo.

REM Get script directory
set SCRIPT_DIR=%~dp0
set ROOT_DIR=%SCRIPT_DIR%..
set BIN_DIR=%ROOT_DIR%\bin
set SRC_DIR=%ROOT_DIR%\src

REM Check if source files exist
if not exist "%SRC_DIR%\Program.cs" (
    echo ERROR: Source files not found in %SRC_DIR%
    echo Please ensure you have cloned the repository correctly
    pause
    exit /b 1
)

echo Step 1: Compiling QuickView...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1"
if %errorLevel% neq 0 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo Step 2: Creating run script...
echo @echo off > "%ROOT_DIR%\run.bat"
echo cd /d "%ROOT_DIR%" >> "%ROOT_DIR%\run.bat"
echo bin\QuickView.exe >> "%ROOT_DIR%\run.bat"

echo.
echo ========================================
echo Setup Complete!
echo ========================================
echo.
echo QuickView has been compiled to: %BIN_DIR%\QuickView.exe
echo.
echo To run QuickView:
echo   - Double-click run.bat
echo   - Or run: bin\QuickView.exe
echo.
echo The application will auto-start on Windows startup after first run.
echo To disable auto-start, delete the shortcut from:
echo   %%APPDATA%%\Microsoft\Windows\Start Menu\Programs\Startup
echo.
echo Starting QuickView now...
start "" "%BIN_DIR%\QuickView.exe"

timeout /t 2 /nobreak >nul
