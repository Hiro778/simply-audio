namespace IvoryIsland.Models;

/// <summary>
/// Identifies a media session's source application truthfully based on its Windows AppUserModelId (AUMID).
/// Enforces browser transparency rules: never pretends a browser session is a native YouTube desktop app,
/// and falls back to a clean browser label when site metadata is unavailable.
/// </summary>
public sealed class SourceAppIdentity
{
    public required string RawAppUserModelId { get; init; }
    public required MediaSourceKind Kind { get; init; }
    public required string ApplicationDisplayName { get; init; }
    public required bool IsBrowserSession { get; init; }
    public required string AccentHex { get; init; }
    public required string GlyphCode { get; init; }

    /// <summary>
    /// Resolves a raw Windows GSMTC SourceAppUserModelId into a structured, truthful SourceAppIdentity.
    /// </summary>
    public static SourceAppIdentity FromAppUserModelId(string? rawAumid, string? currentTitle = null, string? currentArtist = null)
    {
        var aumid = (rawAumid ?? string.Empty).Trim();
        var lower = aumid.ToLowerInvariant();

        if (lower.Contains("spotify"))
        {
            return new SourceAppIdentity
            {
                RawAppUserModelId = aumid,
                Kind = MediaSourceKind.Spotify,
                ApplicationDisplayName = "Spotify",
                IsBrowserSession = false,
                AccentHex = "#1DB954",
                GlyphCode = "\uE93C"
            };
        }

        if (lower.Contains("msedge") || lower.Contains("microsoftedge"))
        {
            return CreateBrowserIdentity(aumid, MediaSourceKind.EdgeBrowser, "Microsoft Edge", currentTitle, currentArtist);
        }

        if (lower.Contains("chrome") && !lower.Contains("chromium"))
        {
            return CreateBrowserIdentity(aumid, MediaSourceKind.ChromeBrowser, "Google Chrome", currentTitle, currentArtist);
        }

        if (lower.Contains("brave"))
        {
            return CreateBrowserIdentity(aumid, MediaSourceKind.BraveBrowser, "Brave Browser", currentTitle, currentArtist);
        }

        if (lower.Contains("vivaldi"))
        {
            return CreateBrowserIdentity(aumid, MediaSourceKind.VivaldiBrowser, "Vivaldi", currentTitle, currentArtist);
        }

        if (lower.Contains("opera"))
        {
            return CreateBrowserIdentity(aumid, MediaSourceKind.OperaBrowser, "Opera", currentTitle, currentArtist);
        }

        if (lower.Contains("firefox") || lower.Contains("mozilla"))
        {
            return CreateBrowserIdentity(aumid, MediaSourceKind.FirefoxBrowser, "Firefox", currentTitle, currentArtist);
        }

        if (lower.Contains("applemusic") || lower.Contains("itunes"))
        {
            return new SourceAppIdentity
            {
                RawAppUserModelId = aumid,
                Kind = MediaSourceKind.AppleMusic,
                ApplicationDisplayName = "Apple Music",
                IsBrowserSession = false,
                AccentHex = "#FA2D48",
                GlyphCode = "\uE8D6"
            };
        }

        if (lower.Contains("vlc"))
        {
            return new SourceAppIdentity
            {
                RawAppUserModelId = aumid,
                Kind = MediaSourceKind.VlcPlayer,
                ApplicationDisplayName = "VLC Media Player",
                IsBrowserSession = false,
                AccentHex = "#FF8800",
                GlyphCode = "\uE714"
            };
        }

        if (lower.Contains("wmplayer") || lower.Contains("zunemusic") || lower.Contains("mediaplayer"))
        {
            return new SourceAppIdentity
            {
                RawAppUserModelId = aumid,
                Kind = MediaSourceKind.WindowsMediaPlayer,
                ApplicationDisplayName = "Media Player",
                IsBrowserSession = false,
                AccentHex = "#242321",
                GlyphCode = "\uE8D6"
            };
        }

        string cleanName = ExtractCleanExecutableOrPackageName(aumid);
        return new SourceAppIdentity
        {
            RawAppUserModelId = aumid,
            Kind = MediaSourceKind.GenericApplication,
            ApplicationDisplayName = cleanName,
            IsBrowserSession = false,
            AccentHex = "#242321",
            GlyphCode = "\uEC4F"
        };
    }

    private static SourceAppIdentity CreateBrowserIdentity(
        string aumid,
        MediaSourceKind browserKind,
        string browserName,
        string? title,
        string? artist)
    {
        // Check if the Windows GSMTC metadata itself explicitly suffixes "- YouTube" or "YouTube Music".
        // Even when it does, we truthfully label it as "<Browser> (YouTube Web)" rather than a fake native YouTube app.
        bool hasExplicitYouTubeHint =
            (!string.IsNullOrWhiteSpace(title) && title.Contains("YouTube", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(artist) && artist.Contains("YouTube", StringComparison.OrdinalIgnoreCase));

        string displayLabel = hasExplicitYouTubeHint
            ? $"{browserName} · YouTube Web"
            : browserName;

        return new SourceAppIdentity
        {
            RawAppUserModelId = aumid,
            Kind = browserKind,
            ApplicationDisplayName = displayLabel,
            IsBrowserSession = true,
            AccentHex = hasExplicitYouTubeHint ? "#E62117" : "#4A6CF7",
            GlyphCode = "\uE774" // Globe / Web Browser glyph
        };
    }

    private static string ExtractCleanExecutableOrPackageName(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid))
            return "Media Source";

        string cleaned = aumid;
        int bangIndex = cleaned.IndexOf('!');
        if (bangIndex > 0)
            cleaned = cleaned[..bangIndex];

        if (cleaned.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^4];

        int lastSep = cleaned.LastIndexOfAny(['\\', '/', '.']);
        if (lastSep >= 0 && lastSep < cleaned.Length - 1)
            cleaned = cleaned[(lastSep + 1)..];

        return string.IsNullOrWhiteSpace(cleaned)
            ? "Media Source"
            : char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }
}
