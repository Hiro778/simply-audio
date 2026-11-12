# Ivory Desktop (`v1.0.0`) — Full Desktop Transformation & Universal Windows Distribution

**Ivory Desktop** (built upon **Ivory Island**) transforms the Windows desktop into a premium, macOS-inspired, white frosted-glass environment. Engineered for Windows 10 (`1809` through `22H2 Build 19045`) and Windows 11 (`x64`) using **C#**, **.NET 8**, **WinUI 3 (Windows App SDK 1.6)**, **Windows.UI.Composition**, and **Windows Core Audio (`WASAPI`)**.

---

## Key Features & Architecture

1. **Global Desktop Menu Bar (`MenuBarWindow`)**
   - Translucent pearl-white glass bar anchored across the top of the display (`32px` height).
   - Apple/Ivory menu flyout with quick system tools, operating mode toggle, and safe exit.
   - Dynamically tracks foreground application titles and standard macOS menus.
   - Integrated media playback pill, real battery percentage & charging status, Wi-Fi connectivity, WASAPI volume glyph, and live clock.
   - Quick toggle button for the Control Center.

2. **Functional macOS-Inspired Glass Dock (`DockWindow`)**
   - Centered translucent glass pill at the bottom of the screen with luminous rim border.
   - **Smooth Magnification**: Gaussian mathematical scaling centered on cursor pointer location (120–180ms duration) with proportional neighbor scaling and no distortion.
   - **Pinned Essentials**: Finder/File Explorer, Launchpad, Web Browser, Windows Terminal, Ivory Island Media, Settings, and Recycle Bin.
   - **Dynamic Running Apps**: Detects running top-level application windows with glowing purple glass indicator dots.

3. **Application Launcher / Launchpad (`LauncherWindow`)**
   - Full-screen frosted acrylic modal with search bar for real-time fuzzy filtering.
   - Discovers genuine installed applications from Start Menu hierarchies and system tools.
   - One-click launch via `Process.Start` and smooth dismissal via `Escape` or clicking empty background.

4. **Global Control Center (`ControlCenterWindow`)**
   - macOS Big Sur / Sonoma-inspired card layout dropping down from the top-right menu bar.
   - **Real Connectivity**: Live Wi-Fi connection name and Bluetooth status with direct system settings integration.
   - **Real WASAPI Audio**: Interactive sound volume slider tied directly to WASAPI master volume with mute toggle.
   - **Display Brightness**: Brightness control with native display settings integration.
   - **Now Playing Widget**: Album art thumbnail, track title, artist, and playback controls (`Play/Pause`, `Prev`, `Next`).
   - **Quick System Tools**: Task Manager (`taskmgr.exe`), Explorer, Taskbar Settings, and Safe Exit.

5. **Desktop Window Overview / Mission Control (`WindowOverviewWindow`)**
   - Enumerates top-level visible application windows using safe Win32 `EnumWindows`.
   - Displays an Exposé-style grid of open application cards.
   - Clicking any window smoothly brings it into focus and restores it (`ShowWindow(SW_RESTORE)` + `SetForegroundWindow`).

6. **Global Bezel Volume HUD (`VolumeHudWindow`)**
   - Sleek centered frosted glass bezel HUD that appears on volume change (140ms fade-in).
   - Displays real-time speaker glyph and volume level bar, auto-dismissing after 1.4 seconds.

7. **Safe Immersive Mode & Emergency Recovery (`Ctrl+Alt+Escape`)**
   - **Operating Modes**: Choose between *Immersive Ivory Desktop* mode and *Standard Windows* mode.
   - **Zero Risk**: Never modifies kernel files, never disables Explorer shell, never touches undocumented Winlogon keys.
   - **Emergency Exit Hotkey (`Ctrl+Alt+Escape`)**: Instantly recovers the desktop, ensures Windows Explorer is running, launches Task Manager, and reverts to Standard mode if needed.

---

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
│       ├── Controls/
│       │   ├── EqualizerBarsControl.xaml
│       │   ├── NavigationRow.xaml
│       │   ├── TabStrip.xaml
│       │   └── TrafficLightControls.xaml
│       ├── Interop/
│       │   └── NativeMethods.cs
│       ├── Models/
│       │   ├── AppSettings.cs
│       │   ├── DesktopModels.cs
│       │   ├── IslandEnums.cs
│       │   ├── MediaSessionSnapshot.cs
│       │   └── SourceAppIdentity.cs
│       ├── Properties/
│       │   └── PublishProfiles/
│       ├── Services/
│       │   ├── AnimationService.cs
│       │   ├── AppDiscoveryService.cs
│       │   ├── ArtworkCache.cs
│       │   ├── AudioVolumeService.cs
│       │   ├── DesktopHostService.cs
│       │   ├── DesktopWindowHelper.cs
│       │   ├── EmergencyRecoveryService.cs
│       │   ├── GlassBackdropService.cs
│       │   ├── MediaSessionManager.cs
│       │   ├── MediaSourceSelector.cs
│       │   ├── SettingsService.cs
│       │   ├── SystemStatusService.cs
│       │   ├── SystemTrayService.cs
│       │   ├── WindowManagementService.cs
│       │   └── WindowManager.cs
│       ├── Themes/
│       │   └── GlassTheme.xaml
│       ├── ViewModels/
│       │   └── IslandViewModel.cs
│       └── Views/
│           ├── ControlCenterWindow.xaml
│           ├── DockWindow.xaml
│           ├── FloatingIslandWindow.xaml
│           ├── LauncherWindow.xaml
│           ├── MenuBarWindow.xaml
│           ├── VolumeHudWindow.xaml
│           ├── WindowOverviewWindow.xaml
│           ├── AudioControlTabView.xaml
│           ├── MediaSourcesTabView.xaml
│           ├── NowPlayingTabView.xaml
│           └── SettingsTabView.xaml
└── tests/
    └── IvoryIsland.Tests/
        ├── IvoryIsland.Tests.csproj
        ├── DesktopTransformationTests.cs
        ├── MultiSourceSelectionTests.cs
        └── TabNavigationTests.cs
```
