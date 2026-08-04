@echo off
title Uninstalling GNS Clock...

echo Closing the clock...
taskkill /im GNSClock.exe /f >nul 2>&1

echo Removing auto-start...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v GNSClock /f >nul 2>&1

echo Removing from Installed Apps list...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\GNSClock" /f >nul 2>&1

echo Removing Start Menu shortcut...
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\GNS Clock.lnk" >nul 2>&1

echo Removing saved settings...
rmdir /s /q "%APPDATA%\GNSClock" >nul 2>&1

echo Removing program files...
start "" /min cmd /c "timeout /t 2 >nul & rmdir /s /q "%LOCALAPPDATA%\GNSClock""

echo.
echo GNS Clock has been uninstalled.
timeout /t 3 >nul
