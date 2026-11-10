# Troubleshooting Guide — Ivory Island (`v1.0.0`)

## 1. Media Session & Browser Detection

### Spotify or Browser Session Not Appearing in the Capsule
- **Cause**: Windows Global System Media Transport Controls (`GSMTC`) only registers a media session once an application has initialized its media player and loaded a track or video.
- **Resolution**:
  - In **Spotify Desktop**: Ensure `Show desktop overlay when using media keys` is enabled under *Spotify Settings -> Display*. Start playing a track once so Spotify registers its GSMTC session with Windows.
  - In **Google Chrome / Microsoft Edge / Brave**: Ensure hardware media key handling is enabled (`chrome://flags/#hardware-media-key-handling` or `edge://flags/#hardware-media-key-handling` set to *Default* or *Enabled*). Note that InPrivate/Incognito tabs deliberately withhold media metadata on some browser configurations for privacy.

### Why Browser Sessions Display `Google Chrome · YouTube Web` or `Microsoft Edge` Instead of a Native YouTube App Icon
- By design, Chromium browsers expose a single browser-level GSMTC session per audio profile/window and do not expose private tab URLs or cookies. Ivory Island inspects only the official Windows `SourceAppUserModelId` and GSMTC `Title`/`Artist` properties, labeling the source truthfully as a browser session rather than faking a native YouTube desktop client.

## 2. Window Visibility & Fullscreen Auto-Hide

### The Floating Capsule Disappeared While Watching a Video or Playing a Game
- **Cause**: Ivory Island's `WindowManager` detects fullscreen applications (`SHQueryUserNotificationState` + foreground monitor bounds matching) and automatically hides the overlay when `Auto-hide in Fullscreen` is enabled.
- **Resolution**: Exit fullscreen mode (`Esc` or `F11`) to restore the capsule automatically, or right-click the **Ivory Island** icon in the Windows System Tray and click **Show Ivory Island** / uncheck **Auto-hide in Fullscreen** in Settings.

### The Capsule Was Dragged Off-Center
- **Resolution**: Double-click the small top drag pill handle above the capsule, click **Reset Position** inside the three-dot (`•••`) Settings drawer, or right-click the System Tray icon and choose **Reset Position to Top Center**.

## 3. Audio & Volume Control

### Volume Slider Moves But Browser Tab Volume Does Not Change Independently
- **Cause**: The main volume slider in Ivory Island controls the **Windows default output endpoint (`IAudioEndpointVolume`)** via WASAPI so it stays 100% synchronized with hardware volume buttons and Windows master volume. Chromium browsers consolidate multiple tabs into shared audio utility processes, so Windows WASAPI only exposes process-level audio (`ISimpleAudioVolume`), not per-tab volume.

## 4. Resetting User Configuration
If you want to reset all saved settings (`settings.json`), delete:
```text
%LOCALAPPDATA%\IvoryIsland\settings.json
```
and restart `IvoryIsland.exe`.
