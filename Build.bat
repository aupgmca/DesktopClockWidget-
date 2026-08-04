@echo off
title Building GNS Clock...
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo ERROR: .NET Framework compiler not found.
    pause
    exit /b 1
)

echo Compiling GNSClock.exe ...
"%CSC%" /nologo /target:winexe /optimize+ /out:GNSClock.exe Clock.cs

if exist GNSClock.exe (
    echo.
    echo SUCCESS! GNSClock.exe created in this folder.
    echo Now run Install.bat to install it.
) else (
    echo.
    echo Build failed - see errors above.
)
pause
