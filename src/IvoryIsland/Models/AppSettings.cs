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

    /// <summary>
    /// Optional user-selected directory for generated cache, artwork thumbnails, diagnostic logs, and exports.
    /// Defaults to %LOCALAPPDATA%\IvoryIsland when null or empty, and automatically falls back if a custom
    /// drive (such as D:\ or an external drive) is disconnected or inaccessible.
    /// </summary>
    public string? CustomDataDirectory { get; set; }
}
