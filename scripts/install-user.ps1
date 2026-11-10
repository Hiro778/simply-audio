<#
.SYNOPSIS
    Per-user non-administrative installer for Ivory Island.
.DESCRIPTION
    Installs Ivory Island into %LOCALAPPDATA%\Programs\IvoryIsland, creates a Start Menu shortcut,
    and optionally registers Ivory Island to start automatically with Windows (HKCU Run).
    Never requires Administrator privileges or UAC elevation.
#>
[CmdletBinding()]
param(
    [switch]$StartWithWindows,
    [switch]$LaunchAfterInstall = $true
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$SourceDir = Join-Path $RepoRoot "dist\IvoryIsland-win-x64"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\IvoryIsland"

if (-not (Test-Path (Join-Path $SourceDir "IvoryIsland.exe"))) {
    Write-Host "Release build not found in $SourceDir. Building Release first..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "build-release.ps1") -Configuration Release
}

Write-Host "Stopping any running IvoryIsland process..." -ForegroundColor Cyan
Get-Process -Name "IvoryIsland" -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Installing Ivory Island to $InstallDir ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force

$ExePath = Join-Path $InstallDir "IvoryIsland.exe"

# Create Start Menu Shortcut
$StartMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$ShortcutPath = Join-Path $StartMenuDir "Ivory Island.lnk"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = $ExePath
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "Minimal Ivory Liquid Glass Floating Media Controller"
$Shortcut.IconLocation = Join-Path $InstallDir "Assets\IvoryIsland.ico"
$Shortcut.Save()

if ($StartWithWindows) {
    $RunKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    Set-ItemProperty -Path $RunKey -Name "IvoryIsland" -Value "`"$ExePath`""
    Write-Host "Registered Ivory Island to start with Windows (Current User)." -ForegroundColor Green
}

Write-Host "Ivory Island installed to $InstallDir" -ForegroundColor Green

if ($LaunchAfterInstall) {
    Start-Process -FilePath $ExePath -WorkingDirectory $InstallDir
}
