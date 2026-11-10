<#
.SYNOPSIS
    Cleanly uninstalls Ivory Island from the current user profile.
#>
[CmdletBinding()]
param(
    [switch]$RemoveUserSettings
)

$ErrorActionPreference = "SilentlyContinue"
Write-Host "Stopping Ivory Island..." -ForegroundColor Cyan
Get-Process -Name "IvoryIsland" | Stop-Process -Force

$RunKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $RunKey -Name "IvoryIsland"

$ShortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Ivory Island.lnk"
if (Test-Path $ShortcutPath) { Remove-Item $ShortcutPath -Force }

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\IvoryIsland"
if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }

if ($RemoveUserSettings) {
    $SettingsDir = Join-Path $env:LOCALAPPDATA "IvoryIsland"
    if (Test-Path $SettingsDir) { Remove-Item $SettingsDir -Recurse -Force }
}

Write-Host "Ivory Island has been uninstalled cleanly." -ForegroundColor Green
