@echo off
setlocal EnableExtensions

fltmc >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Run this script as Administrator.
    goto :Failure
)

set "SERVICE_NAME=AudioPilot"
if not "%~1"=="" set "SERVICE_NAME=%~1"

set "PROJECT_ROOT=%~dp0"
set "PROJECT_FILE=%PROJECT_ROOT%AudioPilot.csproj"
set "PUBLISH_DIR=%PROJECT_ROOT%publish\win-x64"
set "EXECUTABLE=%PUBLISH_DIR%\AudioPilot.exe"

sc.exe query "%SERVICE_NAME%" >nul 2>&1
if not errorlevel 1 (
    echo Removing existing %SERVICE_NAME% service...
    sc.exe stop "%SERVICE_NAME%" >nul 2>&1
    call :WaitForStopped
    if errorlevel 1 goto :Failure

    sc.exe delete "%SERVICE_NAME%"
    if errorlevel 1 (
        echo [ERROR] Unable to delete the existing service.
        goto :Failure
    )
    call :WaitForDeleted
    if errorlevel 1 goto :Failure
)

rem Remove agents left by an older or interrupted service installation.
taskkill.exe /F /IM AudioPilot.exe >nul 2>&1

echo Publishing AudioPilot...
dotnet publish "%PROJECT_FILE%" --configuration Release --runtime win-x64 --self-contained false --output "%PUBLISH_DIR%" -p:PublishSingleFile=true
if errorlevel 1 (
    echo [ERROR] dotnet publish failed.
    goto :Failure
)

echo Creating %SERVICE_NAME% service...
sc.exe create "%SERVICE_NAME%" binPath= "\"%EXECUTABLE%\"" start= auto DisplayName= "AudioPilot"
if errorlevel 1 (
    echo [ERROR] Unable to create the service.
    goto :Failure
)

sc.exe description "%SERVICE_NAME%" "Automatically switches Windows audio output based on the ROG headset wireless link." >nul
sc.exe failure "%SERVICE_NAME%" reset= 86400 actions= restart/5000/restart/15000/restart/30000 >nul
if errorlevel 1 (
    echo [ERROR] Unable to configure service recovery.
    goto :Failure
)

sc.exe start "%SERVICE_NAME%"
if errorlevel 1 (
    echo [ERROR] Unable to start the service.
    goto :Failure
)

echo.
echo AudioPilot service installed and started successfully.
echo Executable: %EXECUTABLE%
goto :Success

:WaitForStopped
for /L %%I in (1,1,15) do (
    sc.exe query "%SERVICE_NAME%" 2>nul | findstr /C:"STOPPED" >nul
    if not errorlevel 1 exit /b 0
    timeout /t 1 /nobreak >nul
)
echo [ERROR] Timed out waiting for the existing service to stop.
exit /b 1

:WaitForDeleted
for /L %%I in (1,1,30) do (
    sc.exe query "%SERVICE_NAME%" >nul 2>&1
    if errorlevel 1 exit /b 0
    timeout /t 1 /nobreak >nul
)
echo [ERROR] Timed out waiting for the existing service to be deleted.
exit /b 1

:Success
echo.
pause
exit /b 0

:Failure
echo.
pause
exit /b 1
