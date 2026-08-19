#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [string]$ServiceName = 'AudioPilot'
)

$ErrorActionPreference = 'Stop'
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($null -eq $service) {
    Write-Host 'AudioPilot service is not installed.'
    exit 0
}

if ($service.Status -ne 'Stopped') {
    Stop-Service -Name $ServiceName -Force
    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(15))
}

& sc.exe delete $ServiceName | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Unable to delete the $ServiceName service."
}

Write-Host 'AudioPilot service uninstalled. Published files were kept.'
