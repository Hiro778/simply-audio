namespace IvoryIsland.Models;

/// <summary>
/// User-configurable settings persisted to %LOCALAPPDATA%\IvoryIsland\settings.json.
/// Strictly separated from the application binary directory so installing, updating,
/// or uninstalling the application never overwrites or deletes user settings.
/// </summary>
public sealed class AppSettings
{
    public bool IsIslandEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public TopPositionAlignment PositionAlignment { get; set; } = TopPositionAlignment.TopCenter;
    public int TopMarginPx { get; set; } = 16;
    public int? CustomX { get; set; }
    public int? CustomY { get; set; }
    public string? PreferredMonitorDeviceName { get; set; }

    /// <summary>Glass opacity between 0.65 and 0.98 (default 0.90 for Ivory Liquid Glass).</summary>
    public double GlassOpacity { get; set; } = 0.90;

    public SourceSelectionMode SelectionMode { get; set; } = SourceSelectionMode.SmartStable;
    public bool UseCompactIdleMode { get; set; } = false;
    public bool AutoHideInFullscreen { get; set; } = true;
    public string? LastSelectedSessionId { get; set; }

    /// <summary>Operating mode: Standard or Immersive Ivory Desktop.</summary>
    public DesktopOperatingMode OperatingMode { get; set; } = DesktopOperatingMode.Immersive;

    /// <summary>Whether the global macOS-style top menu bar is enabled.</summary>
    public bool IsMenuBarEnabled { get; set; } = true;

    /// <summary>Whether the macOS-style bottom dock is enabled.</summary>
    public bool IsDockEnabled { get; set; } = true;

    /// <summary>Whether dock hover magnification is active.</summary>
    public bool DockMagnification { get; set; } = true;

    /// <summary>Whether the dock auto-hides when windows overlap.</summary>
    public bool DockAutoHide { get; set; } = false;

    /// <summary>Show compact Now Playing pill in the top menu bar.</summary>
    public bool ShowMediaInMenuBar { get; set; } = true;

    /// <summary>Show global bezel volume HUD when system volume changes.</summary>
    public bool ShowVolumeHud { get; set; } = true;

    /// <summary>Reduced motion override for accessibility and lower-spec hardware.</summary>
    public bool ReducedMotion { get; set; } = false;

    /// <summary>Safe emergency exit hotkey combination (default Ctrl+Alt+Escape).</summary>
    public string EmergencyExitHotkey { get; set; } = "Ctrl+Alt+Escape";

    /// <summary>
    /// Optional user-selected directory for generated cache, artwork thumbnails, diagnostic logs, and exports.
    /// Defaults to %LOCALAPPDATA%\IvoryIsland when null or empty, and automatically falls back if a custom
    /// drive (such as D:\ or an external drive) is disconnected or inaccessible.
    /// </summary>
    public string? CustomDataDirectory { get; set; }
}
