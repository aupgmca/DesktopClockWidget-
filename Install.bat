@echo off
title Installing GNS Clock (powered by Tech House)...
cd /d "%~dp0"

if not exist GNSClock.exe (
    echo ERROR: GNSClock.exe not found. Run Build.bat first!
    pause
    exit /b 1
)

set DEST=%LOCALAPPDATA%\GNSClock
set STARTMENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs

echo Removing old FliTik Clock version (if any)...
taskkill /im FliTikClock.exe /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v FliTikClock /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\FliTikClock" /f >nul 2>&1
del "%STARTMENU%\FliTik Clock.lnk" >nul 2>&1
rmdir /s /q "%LOCALAPPDATA%\FliTikClock" >nul 2>&1

echo Closing any running clock...
taskkill /im GNSClock.exe /f >nul 2>&1

echo Installing to %DEST% ...
mkdir "%DEST%" 2>nul
copy /y GNSClock.exe "%DEST%\" >nul
copy /y Uninstall.bat "%DEST%\" >nul

echo Setting auto-start with Windows...
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v GNSClock /t REG_SZ /d "\"%DEST%\GNSClock.exe\"" /f >nul

echo Creating Start Menu shortcut...
powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%STARTMENU%\GNS Clock.lnk');$s.TargetPath='%DEST%\GNSClock.exe';$s.Description='GNS Clock - powered by Tech House';$s.Save()"

echo Registering in Installed Apps list...
set UN=HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\GNSClock
reg add "%UN%" /v DisplayName /t REG_SZ /d "GNS Clock (powered by Tech House)" /f >nul
reg add "%UN%" /v DisplayVersion /t REG_SZ /d "3.0" /f >nul
reg add "%UN%" /v Publisher /t REG_SZ /d "Tech House" /f >nul
reg add "%UN%" /v DisplayIcon /t REG_SZ /d "%DEST%\GNSClock.exe" /f >nul
reg add "%UN%" /v InstallLocation /t REG_SZ /d "%DEST%" /f >nul
reg add "%UN%" /v UninstallString /t REG_SZ /d "\"%DEST%\Uninstall.bat\"" /f >nul
reg add "%UN%" /v NoModify /t REG_DWORD /d 1 /f >nul
reg add "%UN%" /v NoRepair /t REG_DWORD /d 1 /f >nul

echo Starting the clock...
start "" "%DEST%\GNSClock.exe"

echo.
echo ============================================
echo  GNS Clock installed successfully!
echo  powered by Tech House
echo  - Auto-starts with Windows
echo  - Listed in Settings ^> Apps ^> Installed apps
echo  - Start Menu shortcut created
echo  You can now delete this build folder if you want.
echo ============================================
pause
