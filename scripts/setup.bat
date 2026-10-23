@echo off
REM QuickView Setup Script
REM This script compiles and installs QuickView on your system

echo ========================================
echo QuickView Setup
echo ========================================
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator
    echo Right-click the file and select "Run as administrator"
    pause
    exit /b 1
)

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
echo Step 2: Creating installation directory...
set INSTALL_DIR=C:\Program Files\QuickView
if not exist "%INSTALL_DIR%" (
    mkdir "%INSTALL_DIR%"
)

echo Step 3: Copying files...
copy /Y "%BIN_DIR%\QuickView.exe" "%INSTALL_DIR%\" >nul
if %errorLevel% neq 0 (
    echo ERROR: Failed to copy files
    pause
    exit /b 1
)

echo Step 4: Creating uninstall script...
echo @echo off > "%INSTALL_DIR%\uninstall.bat"
echo set INSTALL_DIR=C:\Program Files\QuickView >> "%INSTALL_DIR%\uninstall.bat"
echo echo Uninstalling QuickView... >> "%INSTALL_DIR%\uninstall.bat"
echo taskkill /F /IM QuickView.exe 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo timeout /t 2 /nobreak >nul >> "%INSTALL_DIR%\uninstall.bat"
echo del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\QuickView.lnk" 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo rd /s /q "%LOCALAPPDATA%\QuickView" 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo cd /d "%INSTALL_DIR%" >> "%INSTALL_DIR%\uninstall.bat"
echo cd .. >> "%INSTALL_DIR%\uninstall.bat"
echo rd /s /q QuickView >> "%INSTALL_DIR%\uninstall.bat"
echo echo QuickView has been uninstalled >> "%INSTALL_DIR%\uninstall.bat"
echo pause >> "%INSTALL_DIR%\uninstall.bat"

echo Step 5: Creating desktop shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%USERPROFILE%\Desktop\QuickView.lnk'); $s.TargetPath = '%INSTALL_DIR%\QuickView.exe'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'QuickView - System Monitor'; $s.Save()"

echo Step 6: Creating Start Menu shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%APPDATA%\Microsoft\Windows\Start Menu\Programs\QuickView.lnk'); $s.TargetPath = '%INSTALL_DIR%\QuickView.exe'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'QuickView - System Monitor'; $s.Save()"

echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo QuickView has been installed to: %INSTALL_DIR%
echo.
echo Shortcuts created:
echo   - Desktop: QuickView.lnk
echo   - Start Menu: QuickView.lnk
echo.
echo The application will auto-start on Windows login after first run.
echo To disable auto-start, delete QuickView.lnk from:
echo   %%APPDATA%%\Microsoft\Windows\Start Menu\Programs\Startup
echo.
echo To uninstall, run: "%INSTALL_DIR%\uninstall.bat"
echo.
echo Starting QuickView...
start "" "%INSTALL_DIR%\QuickView.exe"

timeout /t 3 /nobreak >nul
