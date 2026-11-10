using IvoryIsland.Models;
using IvoryIsland.Services;
using Xunit;

namespace IvoryIsland.Tests;

/// <summary>
/// Automated verification suite validating Ivory Island's multi-source selection rules,
/// anti-flicker stability when Spotify and YouTube are active simultaneously, truthful
/// browser session identification, and user data directory separation/fallback.
/// </summary>
public sealed class MultiSourceSelectionTests
{
    private static MediaSessionSnapshot CreateSnapshot(
        string sessionId,
        string aumid,
        string title,
        string artist,
        bool isPlaying)
    {
        var identity = SourceAppIdentity.FromAppUserModelId(aumid, title, artist);
        return new MediaSessionSnapshot
        {
            SessionId = sessionId,
            SourceAppUserModelId = aumid,
            Identity = identity,
            Title = title,
            Artist = artist,
            IsPlaying = isPlaying,
            IsPaused = !isPlaying,
            PlaybackStatusText = isPlaying ? "Playing" : "Paused",
            CanTogglePlayPause = true,
            CanSkipNext = true,
            CanSkipPrevious = true
        };
    }

    [Fact]
    public void SpotifyOnly_SelectsSpotifyAndIdentifiesCorrectly()
    {
        var selector = new MediaSourceSelector();
        var spotify = CreateSnapshot("Spotify.exe#01", "Spotify.exe", "Dandelions", "Ruth B.", isPlaying: true);

        var selected = selector.EvaluateSelection([spotify], windowsCurrentSessionId: "Spotify.exe#01", isInitialOrSessionListChanged: true);

        Assert.NotNull(selected);
        Assert.Equal("Spotify.exe#01", selected.SessionId);
        Assert.Equal(MediaSourceKind.Spotify, selected.Identity.Kind);
        Assert.False(selected.Identity.IsBrowserSession);
        Assert.Equal("Spotify", selected.Identity.ApplicationDisplayName);
    }

    [Fact]
    public void BrowserYouTubeSession_NeverPretendsToBeNativeYouTubeApp()
    {
        var chromeYouTube = CreateSnapshot(
            "chrome.exe#02",
            "chrome.exe",
            "Lo-Fi Beats - YouTube",
            "Lofi Girl",
            isPlaying: true);

        var edgeGeneric = CreateSnapshot(
            "msedge.exe#03",
            "MSEdge",
            "Podcast Episode 42",
            "Tech Weekly",
            isPlaying: false);

        Assert.True(chromeYouTube.Identity.IsBrowserSession);
        Assert.Equal(MediaSourceKind.ChromeBrowser, chromeYouTube.Identity.Kind);
        Assert.Contains("Google Chrome", chromeYouTube.Identity.ApplicationDisplayName);

        Assert.True(edgeGeneric.Identity.IsBrowserSession);
        Assert.Equal(MediaSourceKind.EdgeBrowser, edgeGeneric.Identity.Kind);
        Assert.Equal("Microsoft Edge", edgeGeneric.Identity.ApplicationDisplayName);
    }

    [Fact]
    public void SimultaneousSpotifyAndYouTube_ManualSelectionStaysStableAcrossMetadataChanges()
    {
        var selector = new MediaSourceSelector();
        var spotify = CreateSnapshot("Spotify.exe#01", "Spotify.exe", "Dandelions", "Ruth B.", isPlaying: true);
        var youtube = CreateSnapshot("chrome.exe#02", "chrome.exe", "Live Synthwave - YouTube", "Chillsynth", isPlaying: true);
        var list = new List<MediaSessionSnapshot> { spotify, youtube };

        var initial = selector.EvaluateSelection(list, windowsCurrentSessionId: "Spotify.exe#01", isInitialOrSessionListChanged: true);
        Assert.Equal("Spotify.exe#01", initial?.SessionId);

        bool switched = selector.SelectManually("chrome.exe#02", list);
        Assert.True(switched);

        var spotifyNextTrack = CreateSnapshot("Spotify.exe#01", "Spotify.exe", "Midnight City", "M83", isPlaying: true);
        var updatedList = new List<MediaSessionSnapshot> { spotifyNextTrack, youtube };

        var afterMetadataChange = selector.EvaluateSelection(updatedList, windowsCurrentSessionId: "Spotify.exe#01", isInitialOrSessionListChanged: false);

        Assert.Equal("chrome.exe#02", afterMetadataChange?.SessionId);
    }

    [Fact]
    public void SelectedSessionDisconnects_RetainsRemainingSessionGracefully()
    {
        var selector = new MediaSourceSelector();
        var spotify = CreateSnapshot("Spotify.exe#01", "Spotify.exe", "Dandelions", "Ruth B.", isPlaying: true);
        var youtube = CreateSnapshot("msedge.exe#02", "msedge.exe", "Coding Music - YouTube", "Focus", isPlaying: true);

        selector.SelectManually("msedge.exe#02", [spotify, youtube]);
        Assert.Equal("msedge.exe#02", selector.SelectedSessionId);

        var remaining = selector.EvaluateSelection([spotify], windowsCurrentSessionId: "Spotify.exe#01", isInitialOrSessionListChanged: true);

        Assert.NotNull(remaining);
        Assert.Equal("Spotify.exe#01", remaining.SessionId);
        Assert.False(selector.IsManuallyPinned);
    }

    [Fact]
    public void SettingsService_SeparatesUserDataAndFallsBackGracefullyOnUnavailableDrive()
    {
        string tempSettingsFile = Path.Combine(Path.GetTempPath(), $"ivory_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new SettingsService(tempSettingsFile);

            // 1. Default directory points to %LOCALAPPDATA%\IvoryIsland
            Assert.EndsWith("IvoryIsland", service.DefaultDataDirectory, StringComparison.OrdinalIgnoreCase);

            // 2. When a non-existent/unplugged drive path is configured (e.g. Z:\MissingDrive\IvoryData),
            // GetEffectiveDataDirectory() falls back safely to DefaultDataDirectory without throwing.
            service.Current.CustomDataDirectory = @"Z:\NonExistentDrive_9999\IvoryIslandData";
            string effective = service.GetEffectiveDataDirectory();
            Assert.Equal(service.DefaultDataDirectory, effective);
        }
        finally
        {
            if (File.Exists(tempSettingsFile))
                File.Delete(tempSettingsFile);
        }
    }
}
