namespace IvoryIsland.Models;

/// <summary>
/// High-level adaptive visual state of the unified Ivory Island capsule.
/// </summary>
public enum IslandVisualState
{
    /// <summary>No active sessions and no recent volume interaction; capsule is hidden or minimal.</summary>
    IdleHidden,

    /// <summary>No active media session, or user opted for compact idle pill.</summary>
    IdleCompact,

    /// <summary>Temporary compact state displayed inside the SAME capsule when hardware/system volume changes with no active media.</summary>
    VolumeOnlyTransient,

    /// <summary>Active media session is currently playing; full horizontally expanded capsule.</summary>
    MediaPlaying,

    /// <summary>Active media session is paused or stopped; capsule remains visible with paused state indicator.</summary>
    MediaPaused
}

/// <summary>
/// Categorizes the origin application of a Windows GSMTC media session without misrepresenting browser sessions as native apps.
/// </summary>
public enum MediaSourceKind
{
    Spotify,
    ChromeBrowser,
    EdgeBrowser,
    FirefoxBrowser,
    BraveBrowser,
    OperaBrowser,
    VivaldiBrowser,
    AppleMusic,
    VlcPlayer,
    WindowsMediaPlayer,
    GenericApplication
}

/// <summary>
/// Determines how Ivory Island selects among multiple simultaneous media sessions.
/// </summary>
public enum SourceSelectionMode
{
    /// <summary>Follows Windows current session initially, but locks onto user selection and never auto-switches on mere metadata updates.</summary>
    SmartStable,

    /// <summary>Strictly locks onto the manually chosen source until that session closes.</summary>
    ManualLocked
}

/// <summary>
/// Default horizontal anchor position along the top work area of the active display.
/// </summary>
public enum TopPositionAlignment
{
    TopCenter,
    TopLeft,
    TopRight,
    CustomDragged
}
