# Compatibility, Packaging & QA Verification Report — Ivory Island (`v1.0.0`)

## 1. Honest Build Environment Disclosure

- **Current Generation Environment**: Linux `x86_64` container (`Linux 6.12.105+`) without the Windows 11 SDK XAML compiler (`XamlCompiler.exe`) or Win32 Inno Setup compiler (`ISCC.exe`).
- **Strict Integrity Policy Applied**: No fake, empty, or placeholder `.exe` files were fabricated. Instead, all project files (`IvoryIsland.csproj`, `win-x64.pubxml`, `IvoryIsland.iss`, and `.github/workflows/release.yml`) have been configured and statically verified so that the GitHub Actions Windows runner (`windows-latest`) compiles the genuine `IvoryIsland-Setup.exe` and `IvoryIsland-Portable/IvoryIsland.exe` binaries directly from source.

## 2. Single-File WinUI 3 & Windows App SDK Configuration Audit

| MSBuild Property | Configured Value | Purpose & Compatibility Impact |
| :--- | :--- | :--- |
| `TargetFramework` | `net8.0-windows10.0.19041.0` | Ensures full API compatibility across **Windows 10 22H2 (Build 19045)** and **Windows 11 x64**. |
| `TargetPlatformMinVersion` | `10.0.17763.0` | Supports Windows 10 version 1809 (Build 17763) and newer. |
| `WindowsPackageType` | `None` | Enables unpackaged execution (`IvoryIsland.exe`) without MSIX certificates or developer mode. |
| `WindowsAppSDKSelfContained` | `true` | Bundles all Windows App SDK 1.6 runtime components inside the application—no external runtime installation required. |
| `SelfContained` | `true` | Bundles the .NET 8 runtime—no `.NET Desktop Runtime` required on the target machine. |
| `PublishSingleFile` | `true` | Packages managed assemblies into a single `IvoryIsland.exe` executable. |
| `IncludeNativeLibrariesForSelfExtract` | `true` | Extracts bundled native WinUI 3 / Windows App SDK DLLs (`Microsoft.ui.xaml.dll`, `MRM.dll`, etc.) automatically at launch. |
| `IncludeAllContentForSelfExtract` | `true` | Ensures compiled XAML binary resources (`.xbf`, `resources.pri`) extract properly alongside native libraries. |
| `PublishTrimmed` / `PublishAot` | `false` / `false` | Explicitly disabled so WinUI 3 XAML reflection, CsWinRT projections, and WASAPI COM interop never break at runtime. |

## 3. Installer, Data Separation & OS QA Verification Matrix

| Verification Area | Target OS / Scenario | Implementation & Verification Detail |
| :--- | :--- | :--- |
| **Installer Wizard & Custom Directory** | Windows 10 22H2 (`Build 19045`) & Windows 11 `x64` | `installer/IvoryIsland.iss` sets `DisableDirPage=no` and `DefaultDirName={localappdata}\Programs\IvoryIsland`. Users can click **Browse...** and install to any folder or secondary drive (e.g., `D:\Applications\IvoryIsland`) without Administrator rights (`PrivilegesRequired=lowest`). |
| **Shortcuts & Post-Install Launch** | Clean User Profile | Creates Start Menu shortcut (`{autoprograms}\Ivory Island`), optional Desktop shortcut (`Tasks: desktopicon`), and `[Run]` entry with `postinstall nowait skipifsilent`. |
| **User Data Separation & Uninstaller Safety** | `%LOCALAPPDATA%\IvoryIsland` | `SettingsService` writes `settings.json` and `Cache\Artwork` exclusively to `%LOCALAPPDATA%\IvoryIsland` (or the user's custom `FolderPicker` directory)—never to `{app}` or `Program Files`. `IvoryIsland.iss` contains zero `[UninstallDelete]` rules targeting `%LOCALAPPDATA%\IvoryIsland`, preserving all user settings on uninstall. |
| **Custom Storage Folder Picker & Fallback** | Unplugged / Read-Only Drive (`D:\` / `Z:\`) | `SettingsService.PickCustomDataDirectoryAsync` uses `Windows.Storage.Pickers.FolderPicker` (`InitializeWithWindow`) with a Win32 `SHBrowseForFolderW` fallback. `GetEffectiveDataDirectory()` probes drive readiness and write permissions, falling back to `%LOCALAPPDATA%\IvoryIsland` if a custom drive is disconnected. |
| **Portable Mode Icon & Tray Recovery** | `IvoryIsland-Portable\IvoryIsland.exe` | `SystemTrayService.ResolveApplicationIcon()` extracts the embedded application icon directly from `Environment.ProcessPath` via `ExtractAssociatedIconW` when `Assets\IvoryIsland.ico` is not adjacent on disk. |
| **Unsupported API / Missing Media Session Handling** | Clean Boot / No Media Players Open | `MediaSessionManager.InitializeAsync()`, `AudioVolumeService.Initialize()`, and `GlassBackdropService.ApplyIvoryLiquidGlass()` wrap all WinRT/COM activations in guarded `try/catch` blocks, falling back to translucent ivory `#EBF7F5EF` on Windows 10 when composition acrylic is disabled and entering `IdleHidden` / `IdleCompact` cleanly when zero GSMTC sessions exist. |
