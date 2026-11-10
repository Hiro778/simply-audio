<#
.SYNOPSIS
    Builds and packages Ivory Island into the exact Release/ distribution structure:
    Release/
    ├── IvoryIsland-Setup.exe
    ├── IvoryIsland-Portable/
    │   └── IvoryIsland.exe
    ├── Checksums-SHA256.txt
    └── README-INSTALL.txt
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjPath = Join-Path $RepoRoot "src\IvoryIsland\IvoryIsland.csproj"
$TestProjPath = Join-Path $RepoRoot "tests\IvoryIsland.Tests\IvoryIsland.Tests.csproj"
$PublishStageDir = Join-Path $RepoRoot "publish\win-x64-single"
$ReleaseRoot = Join-Path $RepoRoot "Release"
$PortableDir = Join-Path $ReleaseRoot "IvoryIsland-Portable"
$IssPath = Join-Path $RepoRoot "installer\IvoryIsland.iss"
$ReadmeInstallPath = Join-Path $ReleaseRoot "README-INSTALL.txt"
$ChecksumsPath = Join-Path $ReleaseRoot "Checksums-SHA256.txt"

Write-Host "=== Ivory Island Release Build & Packaging Pipeline ===" -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 8.0 SDK (dotnet.exe) was not found in PATH."
}

# 1. Restore NuGet packages for both main app and test project
Write-Host "[1/6] Restoring NuGet packages (win-x64)..." -ForegroundColor Yellow
dotnet restore $ProjPath -r win-x64
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed for IvoryIsland.csproj" }

if (-not $SkipTests) {
    dotnet restore $TestProjPath -r win-x64
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed for IvoryIsland.Tests.csproj" }

    # 2. Run xUnit test suite
    Write-Host "[2/6] Running xUnit verification tests..." -ForegroundColor Yellow
    dotnet test $TestProjPath -c $Configuration -r win-x64 -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw "xUnit test suite failed." }
}

# 3. Publish self-contained, unpackaged, single-file WinUI 3 application
Write-Host "[3/6] Publishing self-contained unpackaged single-file WinUI 3 app (win-x64)..." -ForegroundColor Yellow
if (Test-Path $PublishStageDir) {
    Remove-Item -Path $PublishStageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $PublishStageDir -Force | Out-Null

dotnet publish $ProjPath `
    -c $Configuration `
    -r win-x64 `
    -p:Platform=x64 `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:PublishAot=false `
    -p:PublishReadyToRun=false `
    -o $PublishStageDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for IvoryIsland.csproj"
}

$PublishedExe = Join-Path $PublishStageDir "IvoryIsland.exe"
if (-not (Test-Path $PublishedExe)) {
    throw "Expected published binary not found at: $PublishedExe"
}

# 4. Populate Release/IvoryIsland-Portable/
Write-Host "[4/6] Assembling Release/IvoryIsland-Portable/..." -ForegroundColor Yellow
if (Test-Path $PortableDir) {
    Remove-Item -Path $PortableDir -Recurse -Force
}
New-Item -ItemType Directory -Path $PortableDir -Force | Out-Null

# Copy IvoryIsland.exe and any required WinUI PRI/native runtime files emitted by Windows App SDK
Copy-Item -Path "$PublishStageDir\*" -Destination $PortableDir -Recurse -Force
Get-ChildItem -Path $PortableDir -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

$PortableExePath = Join-Path $PortableDir "IvoryIsland.exe"
if (-not (Test-Path $PortableExePath)) {
    throw "Portable executable missing at: $PortableExePath"
}

# 5. Compile installer/IvoryIsland.iss using Inno Setup (ISCC.exe)
Write-Host "[5/6] Compiling Release/IvoryIsland-Setup.exe with Inno Setup..." -ForegroundColor Yellow
$IsccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
)
$IsccExe = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $IsccExe) {
    $IsccCmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($IsccCmd) { $IsccExe = $IsccCmd.Source }
}

if (-not $IsccExe) {
    throw "Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6 ('choco install innosetup -y' or 'winget install JRSoftware.InnoSetup')."
}

$SetupExePath = Join-Path $ReleaseRoot "IvoryIsland-Setup.exe"
if (Test-Path $SetupExePath) {
    Remove-Item -Path $SetupExePath -Force
}

& $IsccExe $IssPath
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $SetupExePath)) {
    throw "Inno Setup compilation failed to produce $SetupExePath"
}

# Remove placeholder note if present
$AwaitNote = Join-Path $ReleaseRoot "BUILD_AWAITING_WINDOWS_RUNNER.txt"
if (Test-Path $AwaitNote) {
    Remove-Item -Path $AwaitNote -Force
}

# 6. Generate Checksums-SHA256.txt and verify README-INSTALL.txt
Write-Host "[6/6] Generating Release/Checksums-SHA256.txt..." -ForegroundColor Yellow
$SetupHash = (Get-FileHash -Path $SetupExePath -Algorithm SHA256).Hash.ToLowerInvariant()
$PortableHash = (Get-FileHash -Path $PortableExePath -Algorithm SHA256).Hash.ToLowerInvariant()

$ChecksumLines = @(
    "$SetupHash  IvoryIsland-Setup.exe",
    "$PortableHash  IvoryIsland-Portable/IvoryIsland.exe"
)
Set-Content -Path $ChecksumsPath -Value ($ChecksumLines -join "`r`n") -Encoding UTF8

if (-not (Test-Path $ReadmeInstallPath)) {
    throw "Required file missing: $ReadmeInstallPath"
}

Write-Host "=== Release Build Completed Successfully ===" -ForegroundColor Green
Get-ChildItem -Path $ReleaseRoot -Recurse | Where-Object { -not $_.PSIsContainer } | Select-Object FullName, Length
