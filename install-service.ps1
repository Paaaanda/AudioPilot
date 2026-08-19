#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [string]$ServiceName = 'AudioPilot'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot 'AudioPilot.csproj'
$publishDirectory = Join-Path $projectRoot 'publish\win-x64'
$executable = Join-Path $publishDirectory 'AudioPilot.exe'

Write-Host 'Publishing AudioPilot...'
dotnet publish $projectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $publishDirectory `
    -p:PublishSingleFile=true

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $existingService) {
    if ($existingService.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force
        $existingService.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(15))
    }

    & sc.exe delete $ServiceName | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to delete the existing $ServiceName service."
    }

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($null -eq (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
            break
        }
        Start-Sleep -Milliseconds 500
    }
}

$binaryPath = '"' + $executable + '"'
New-Service `
    -Name $ServiceName `
    -BinaryPathName $binaryPath `
    -DisplayName 'AudioPilot' `
    -Description 'Automatically switches Windows audio output based on the ROG headset wireless link.' `
    -StartupType Automatic | Out-Null

& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/15000/restart/30000 | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Unable to configure recovery for $ServiceName."
}

Start-Service -Name $ServiceName
(Get-Service -Name $ServiceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(15))

Write-Host 'AudioPilot service installed and running.'
Write-Host "Executable: $executable"
