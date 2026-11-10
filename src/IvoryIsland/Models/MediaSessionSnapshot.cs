using Microsoft.UI.Xaml.Media.Imaging;

namespace IvoryIsland.Models;

/// <summary>
/// Immutable point-in-time snapshot of a single Windows GSMTC media session's metadata,
/// playback state, timeline, and supported transport capabilities.
/// </summary>
public sealed class MediaSessionSnapshot
{
    public required string SessionId { get; init; }
    public required string SourceAppUserModelId { get; init; }
    public required SourceAppIdentity Identity { get; init; }

    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string AlbumTitle { get; init; } = string.Empty;

    public bool IsPlaying { get; init; }
    public bool IsPaused { get; init; }
    public string PlaybackStatusText { get; init; } = "Paused";

    // Transport capabilities exposed by the target session
    public bool CanPlay { get; init; }
    public bool CanPause { get; init; }
    public bool CanTogglePlayPause { get; init; }
    public bool CanSkipNext { get; init; }
    public bool CanSkipPrevious { get; init; }
    public bool CanSeek { get; init; }
    public bool CanShuffle { get; init; }
    public bool CanRepeat { get; init; }

    public bool IsShuffleActive { get; init; }
    public string RepeatMode { get; init; } = "None";

    public TimeSpan Position { get; init; } = TimeSpan.Zero;
    public TimeSpan Duration { get; init; } = TimeSpan.Zero;
    public DateTimeOffset TimelineLastUpdated { get; init; } = DateTimeOffset.UtcNow;

    public string? ArtworkCacheKey { get; init; }
    public BitmapImage? ArtworkImage { get; init; }

    public string DisplayTitle =>
        !string.IsNullOrWhiteSpace(Title)
            ? Title
            : Identity.ApplicationDisplayName;

    public string DisplaySubtitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Artist))
                return Artist;
            if (!string.IsNullOrWhiteSpace(AlbumTitle))
                return AlbumTitle;
            return Identity.ApplicationDisplayName;
        }
    }
}
