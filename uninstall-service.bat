@echo off
setlocal EnableExtensions

fltmc >nul 2>&1
if errorlevel 1 (
    echo [错误] 请右键选择“以管理员身份运行”。
    goto :Failure
)

set "SERVICE_NAME=AudioPilot"
if not "%~1"=="" set "SERVICE_NAME=%~1"

sc.exe query "%SERVICE_NAME%" >nul 2>&1
if errorlevel 1 (
    echo AudioPilot 服务尚未安装。
    goto :StopAgents
)

echo 正在停止 %SERVICE_NAME% 服务...
sc.exe stop "%SERVICE_NAME%" >nul 2>&1
call :WaitForStopped
if errorlevel 1 goto :Failure

echo 正在删除 %SERVICE_NAME% 服务...
sc.exe delete "%SERVICE_NAME%"
if errorlevel 1 (
    echo [错误] 无法删除服务。
    goto :Failure
)
call :WaitForDeleted
if errorlevel 1 goto :Failure

:StopAgents
echo 正在停止残留的 AudioPilot 代理进程...
taskkill.exe /F /IM AudioPilot.exe >nul 2>&1
echo AudioPilot 服务已卸载，发布文件已保留。
goto :Success

:WaitForStopped
for /L %%I in (1,1,15) do (
    sc.exe query "%SERVICE_NAME%" 2>nul | findstr /C:"STOPPED" >nul
    if not errorlevel 1 exit /b 0
    timeout /t 1 /nobreak >nul
)
echo [错误] 等待服务停止超时。
exit /b 1

:WaitForDeleted
for /L %%I in (1,1,30) do (
    sc.exe query "%SERVICE_NAME%" >nul 2>&1
    if errorlevel 1 exit /b 0
    timeout /t 1 /nobreak >nul
)
echo [错误] 等待服务删除超时。
exit /b 1

:Success
echo.
pause
exit /b 0

:Failure
echo.
pause
exit /b 1
