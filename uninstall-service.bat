@echo off
setlocal EnableExtensions

fltmc >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Run this script as Administrator.
    exit /b 1
)

set "SERVICE_NAME=AudioPilot"
if not "%~1"=="" set "SERVICE_NAME=%~1"

sc.exe query "%SERVICE_NAME%" >nul 2>&1
if errorlevel 1 (
    echo AudioPilot service is not installed.
    exit /b 0
)

echo Stopping %SERVICE_NAME% service...
sc.exe stop "%SERVICE_NAME%" >nul 2>&1
call :WaitForStopped

echo Deleting %SERVICE_NAME% service...
sc.exe delete "%SERVICE_NAME%"
if errorlevel 1 (
    echo [ERROR] Unable to delete the service.
    exit /b 1
)

echo AudioPilot service uninstalled. Published files were kept.
exit /b 0

:WaitForStopped
for /L %%I in (1,1,15) do (
    sc.exe query "%SERVICE_NAME%" 2>nul | findstr /C:"STOPPED" >nul
    if not errorlevel 1 exit /b 0
    timeout /t 1 /nobreak >nul
)
exit /b 0
