# Ivory Island (`v1.0.0`) — Universal Windows 10 (22H2 Build 19045) & Windows 11 Distribution

**Ivory Island** is a native Windows 10 (`1809` through `22H2 Build 19045`) and Windows 11 (`x64`) floating media and volume controller built with **C#**, **.NET 8**, **WinUI 3 (Windows App SDK 1.6)**, **Windows Runtime (`Windows.Media.Control`)**, and **Windows Core Audio (`WASAPI`)**.

It is engineered for **Zero-Prerequisite End-User Distribution**:
- **One-Click Installer (`Release/IvoryIsland-Setup.exe`)**: Built with Inno Setup 6 (`PrivilegesRequired=lowest`), featuring a modern wizard, Ivory Island branding, a **Browse...** directory selector defaulting to `%LOCALAPPDATA%\Programs\IvoryIsland` (and supporting any custom path/drive such as `D:\Applications\IvoryIsland`), Start Menu and optional Desktop shortcuts, post-install launch, and a clean uninstaller that preserves user data in `%LOCALAPPDATA%\IvoryIsland`.
- **Single-File Portable EXE (`Release/IvoryIsland-Portable/IvoryIsland.exe`)**: Configured with `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, and `IncludeAllContentForSelfExtract=true`. Runs on clean Windows 10 and Windows 11 machines without requiring `.NET SDK`, `.NET Desktop Runtime`, or `Windows App SDK Runtime`.
- **Cloud Build Without Local .NET SDK (`.github/workflows/release.yml`)**: Because local `.NET SDK` installation is not required on your development PC, the included GitHub Actions workflow compiles the project on a Windows runner (`windows-latest`), executes the xUnit test suite, publishes the single-file `IvoryIsland.exe`, compiles `IvoryIsland-Setup.exe` via Inno Setup, generates `Checksums-SHA256.txt`, and uploads the ready-to-run `Release/` package.

## Target Release Structure

```text
Release/
├── IvoryIsland-Setup.exe
├── IvoryIsland-Portable/
│   └── IvoryIsland.exe
├── Checksums-SHA256.txt
└── README-INSTALL.txt
```

## Complete Repository Structure

```text
IvoryIsland/
├── .github/
│   └── workflows/
│       └── release.yml
├── IvoryIsland.sln
├── README.md
├── Release/
│   ├── README-INSTALL.txt
│   ├── Checksums-SHA256.txt
│   └── IvoryIsland-Portable/
├── docs/
│   ├── BUILD_INSTRUCTIONS.md
│   ├── COMPATIBILITY_TEST_REPORT.md
│   ├── PERFORMANCE_AND_COMPATIBILITY.md
│   └── TROUBLESHOOTING.md
├── installer/
│   └── IvoryIsland.iss
├── scripts/
│   ├── build-release.ps1
│   ├── install-user.ps1
│   └── uninstall-user.ps1
├── src/
│   └── IvoryIsland/
│       ├── IvoryIsland.csproj
│       ├── app.manifest
│       ├── Package.appxmanifest
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── MainWindow.xaml
│       ├── MainWindow.xaml.cs
│       ├── Assets/
│       │   ├── IvoryIsland.ico
│       │   ├── IvoryIsland.png
│       │   ├── WizardImage.bmp
│       │   └── WizardSmallImage.bmp
│       ├── Interop/
│       │   └── NativeMethods.cs
│       ├── Models/
│       │   ├── AppSettings.cs
│       │   ├── IslandEnums.cs
│       │   ├── MediaSessionSnapshot.cs
│       │   └── SourceAppIdentity.cs
│       ├── Properties/
│       │   └── PublishProfiles/
│       │       └── win-x64.pubxml
│       ├── Services/
│       │   ├── AnimationService.cs
│       │   ├── ArtworkCache.cs
│       │   ├── AudioVolumeService.cs
│       │   ├── GlassBackdropService.cs
│       │   ├── MediaSessionController.cs
│       │   ├── MediaSessionManager.cs
│       │   ├── MediaSourceSelector.cs
│       │   ├── SettingsService.cs
│       │   ├── SystemTrayService.cs
│       │   └── WindowManager.cs
│       ├── ViewModels/
│       │   └── IslandViewModel.cs
│       └── Views/
│           ├── FloatingIslandView.xaml
│           └── FloatingIslandView.xaml.cs
└── tests/
    └── IvoryIsland.Tests/
        ├── IvoryIsland.Tests.csproj
        └── MultiSourceSelectionTests.cs
```
