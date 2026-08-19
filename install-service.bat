@echo off
setlocal EnableExtensions

fltmc >nul 2>&1
if errorlevel 1 (
    echo [错误] 请右键选择“以管理员身份运行”。
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
    echo 正在移除现有的 %SERVICE_NAME% 服务...
    sc.exe stop "%SERVICE_NAME%" >nul 2>&1
    call :WaitForStopped
    if errorlevel 1 goto :Failure

    sc.exe delete "%SERVICE_NAME%"
    if errorlevel 1 (
        echo [错误] 无法删除现有服务。
        goto :Failure
    )
    call :WaitForDeleted
    if errorlevel 1 goto :Failure
)

rem 清理由旧版本或安装中断遗留的代理进程。
taskkill.exe /F /IM AudioPilot.exe >nul 2>&1

echo 正在发布 AudioPilot...
dotnet publish "%PROJECT_FILE%" --configuration Release --runtime win-x64 --self-contained false --output "%PUBLISH_DIR%" -p:PublishSingleFile=true
if errorlevel 1 (
    echo [错误] dotnet publish 发布失败。
    goto :Failure
)

echo 正在创建 %SERVICE_NAME% 服务...
sc.exe create "%SERVICE_NAME%" binPath= "\"%EXECUTABLE%\"" start= auto DisplayName= "AudioPilot"
if errorlevel 1 (
    echo [错误] 无法创建服务。
    goto :Failure
)

sc.exe description "%SERVICE_NAME%" "Automatically switches Windows audio output based on the ROG headset wireless link." >nul
sc.exe failure "%SERVICE_NAME%" reset= 86400 actions= restart/5000/restart/15000/restart/30000 >nul
if errorlevel 1 (
    echo [错误] 无法配置服务故障恢复。
    goto :Failure
)

sc.exe start "%SERVICE_NAME%"
if errorlevel 1 (
    echo [错误] 无法启动服务。
    goto :Failure
)

echo.
echo AudioPilot 服务已成功安装并启动。
echo 程序路径：%EXECUTABLE%
goto :Success

:WaitForStopped
for /L %%I in (1,1,15) do (
    sc.exe query "%SERVICE_NAME%" 2>nul | findstr /C:"STOPPED" >nul
    if not errorlevel 1 exit /b 0
    timeout /t 1 /nobreak >nul
)
echo [错误] 等待现有服务停止超时。
exit /b 1

:WaitForDeleted
for /L %%I in (1,1,30) do (
    sc.exe query "%SERVICE_NAME%" >nul 2>&1
    if errorlevel 1 exit /b 0
    timeout /t 1 /nobreak >nul
)
echo [错误] 等待现有服务删除超时。
exit /b 1

:Success
echo.
pause
exit /b 0

:Failure
echo.
pause
exit /b 1
