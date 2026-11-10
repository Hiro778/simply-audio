using System.Runtime.InteropServices;
using IvoryIsland.Models;
using Windows.Media;
using Windows.Media.Control;

namespace IvoryIsland.Services;

/// <summary>
/// Wraps a single Windows GlobalSystemMediaTransportControlsSession.
/// Subscribes to real-time metadata, playback state, and timeline events, and ensures
/// all event subscriptions are detached when the session closes.
/// </summary>
public sealed class MediaSessionController : IDisposable
{
    private readonly GlobalSystemMediaTransportControlsSession _session;
    private readonly ArtworkCache _artworkCache;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private CancellationTokenSource? _refreshCts;
    private MediaSessionSnapshot _latestSnapshot;
    private bool _isDisposed;

    public string SessionId { get; }
    public string SourceAppUserModelId { get; }

    public MediaSessionSnapshot LatestSnapshot => _latestSnapshot;

    public event EventHandler<MediaSessionSnapshot>? SnapshotUpdated;

    public MediaSessionController(
        string sessionId,
        GlobalSystemMediaTransportControlsSession session,
        ArtworkCache artworkCache)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _artworkCache = artworkCache ?? throw new ArgumentNullException(nameof(artworkCache));
        SourceAppUserModelId = SafeGetAumid(session);

        var initialIdentity = SourceAppIdentity.FromAppUserModelId(SourceAppUserModelId);
        _latestSnapshot = new MediaSessionSnapshot
        {
            SessionId = SessionId,
            SourceAppUserModelId = SourceAppUserModelId,
            Identity = initialIdentity,
            Title = initialIdentity.ApplicationDisplayName,
            Artist = "Ready"
        };

        _session.MediaPropertiesChanged += OnSessionMediaPropertiesChanged;
        _session.PlaybackInfoChanged += OnSessionPlaybackInfoChanged;
        _session.TimelinePropertiesChanged += OnSessionTimelinePropertiesChanged;
    }

    private static string SafeGetAumid(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.SourceAppUserModelId ?? "UnknownApp";
        }
        catch
        {
            return "UnknownApp";
        }
    }

    private void OnSessionMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = TriggerRefreshAsync(includeMediaProperties: true);
    }

    private void OnSessionPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = TriggerRefreshAsync(includeMediaProperties: false);
    }

    private void OnSessionTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = TriggerRefreshAsync(includeMediaProperties: false);
    }

    public async Task<MediaSessionSnapshot> TriggerRefreshAsync(bool includeMediaProperties = true)
    {
        if (_isDisposed)
            return _latestSnapshot;

        CancellationTokenSource cts;
        lock (_refreshLock)
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();
            cts = _refreshCts;
        }

        CancellationToken token = cts.Token;

        try
        {
            // Small coalescing delay (25ms) to batch burst events when a track starts playing
            await Task.Delay(25, token).ConfigureAwait(false);
            await _refreshLock.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return _latestSnapshot;
        }
        catch (ObjectDisposedException)
        {
            return _latestSnapshot;
        }

        try
        {
            if (_isDisposed || token.IsCancellationRequested)
                return _latestSnapshot;

            var prev = _latestSnapshot;
            string title = prev.Title;
            string artist = prev.Artist;
            string albumTitle = prev.AlbumTitle;
            string? artworkKey = prev.ArtworkCacheKey;
            var artworkImage = prev.ArtworkImage;

            if (includeMediaProperties || string.IsNullOrWhiteSpace(title))
            {
                try
                {
                    var props = await _session.TryGetMediaPropertiesAsync().AsTask(token).ConfigureAwait(false);
                    if (props is not null)
                    {
                        title = (props.Title ?? string.Empty).Trim();
                        artist = (props.Artist ?? string.Empty).Trim();
                        albumTitle = (props.AlbumTitle ?? string.Empty).Trim();

                        if (props.Thumbnail is not null)
                        {
                            var (decodedKey, decodedImg) = await _artworkCache.GetOrDecodeAsync(
                                props.Thumbnail,
                                $"{SourceAppUserModelId}:{title}:{artist}",
                                token).ConfigureAwait(false);

                            artworkKey = decodedKey;
                            artworkImage = decodedImg;
                        }
                        else
                        {
                            artworkKey = null;
                            artworkImage = null;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return _latestSnapshot;
                }
                catch (COMException)
                {
                    // Session may be transitioning or unsupported
                }
                catch
                {
                    // Keep previous metadata if transient WinRT error occurs
                }
            }

            bool isPlaying = false;
            bool isPaused = true;
            string statusText = "Paused";
            bool canPlay = false;
            bool canPause = false;
            bool canToggle = false;
            bool canNext = false;
            bool canPrev = false;
            bool canSeek = false;
            bool canShuffle = false;
            bool canRepeat = false;
            bool isShuffle = false;
            string repeatMode = "None";

            try
            {
                var playbackInfo = _session.GetPlaybackInfo();
                if (playbackInfo is not null)
                {
                    var status = playbackInfo.PlaybackStatus;
                    isPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    isPaused = status is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused
                        or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped
                        or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;
                    statusText = status.ToString();

                    isShuffle = playbackInfo.IsShuffleActive ?? false;
                    repeatMode = playbackInfo.AutoRepeatMode?.ToString() ?? "None";

                    var controls = playbackInfo.Controls;
                    if (controls is not null)
                    {
                        canPlay = controls.IsPlayEnabled;
                        canPause = controls.IsPauseEnabled;
                        canToggle = controls.IsPlayPauseToggleEnabled || canPlay || canPause;
                        canNext = controls.IsNextEnabled;
                        canPrev = controls.IsPreviousEnabled;
                        canSeek = controls.IsPlaybackPositionEnabled;
                        canShuffle = controls.IsShuffleEnabled;
                        canRepeat = controls.IsRepeatEnabled;
                    }
                }
            }
            catch
            {
                // Ignore if session disconnected mid-query
            }

            TimeSpan position = prev.Position;
            TimeSpan duration = prev.Duration;
            DateTimeOffset timelineUpdated = prev.TimelineLastUpdated;

            try
            {
                var timeline = _session.GetTimelineProperties();
                if (timeline is not null)
                {
                    position = timeline.Position;
                    duration = timeline.EndTime > timeline.StartTime
                        ? timeline.EndTime - timeline.StartTime
                        : TimeSpan.Zero;
                    timelineUpdated = timeline.LastUpdatedTime;
                }
            }
            catch
            {
                // Timeline optional on some browser sources
            }

            var identity = SourceAppIdentity.FromAppUserModelId(SourceAppUserModelId, title, artist);

            var nextSnapshot = new MediaSessionSnapshot
            {
                SessionId = SessionId,
                SourceAppUserModelId = SourceAppUserModelId,
                Identity = identity,
                Title = title,
                Artist = artist,
                AlbumTitle = albumTitle,
                IsPlaying = isPlaying,
                IsPaused = isPaused,
                PlaybackStatusText = statusText,
                CanPlay = canPlay,
                CanPause = canPause,
                CanTogglePlayPause = canToggle,
                CanSkipNext = canNext,
                CanSkipPrevious = canPrev,
                CanSeek = canSeek,
                CanShuffle = canShuffle,
                CanRepeat = canRepeat,
                IsShuffleActive = isShuffle,
                RepeatMode = repeatMode,
                Position = position,
                Duration = duration,
                TimelineLastUpdated = timelineUpdated,
                ArtworkCacheKey = artworkKey,
                ArtworkImage = artworkImage
            };

            _latestSnapshot = nextSnapshot;
            SnapshotUpdated?.Invoke(this, nextSnapshot);
            return nextSnapshot;
        }
        finally
        {
            if (!_isDisposed)
            {
                try { _refreshLock.Release(); } catch { }
            }
        }
    }

    public async Task<bool> TryTogglePlayPauseAsync()
    {
        if (_isDisposed) return false;
        try
        {
            var snap = _latestSnapshot;
            if (snap.IsPlaying && snap.CanPause)
                return await _session.TryPauseAsync().AsTask().ConfigureAwait(false);
            if (!snap.IsPlaying && snap.CanPlay)
                return await _session.TryPlayAsync().AsTask().ConfigureAwait(false);
            return await _session.TryTogglePlayPauseAsync().AsTask().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryPlayAsync()
    {
        if (_isDisposed) return false;
        try
        {
            return await _session.TryPlayAsync().AsTask().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryPauseAsync()
    {
        if (_isDisposed) return false;
        try
        {
            return await _session.TryPauseAsync().AsTask().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TrySkipNextAsync()
    {
        if (_isDisposed || !_latestSnapshot.CanSkipNext) return false;
        try
        {
            return await _session.TrySkipNextAsync().AsTask().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TrySkipPreviousAsync()
    {
        if (_isDisposed || !_latestSnapshot.CanSkipPrevious) return false;
        try
        {
            return await _session.TrySkipPreviousAsync().AsTask().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryChangePlaybackPositionAsync(TimeSpan requestedPosition)
    {
        if (_isDisposed || !_latestSnapshot.CanSeek) return false;
        try
        {
            return await _session.TryChangePlaybackPositionAsync(requestedPosition.Ticks).AsTask().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryToggleShuffleAsync()
    {
        if (_isDisposed || !_latestSnapshot.CanShuffle) return false;
        try
        {
            return await _session.TryChangeShuffleActiveAsync(!_latestSnapshot.IsShuffleActive).AsTask().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryCycleRepeatModeAsync()
    {
        if (_isDisposed || !_latestSnapshot.CanRepeat) return false;
        try
        {
            var nextMode = _latestSnapshot.RepeatMode switch
            {
                "None" => MediaPlaybackAutoRepeatMode.List,
                "List" => MediaPlaybackAutoRepeatMode.Track,
                _ => MediaPlaybackAutoRepeatMode.None
            };
            return await _session.TryChangeAutoRepeatModeAsync(nextMode).AsTask().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        try
        {
            _session.MediaPropertiesChanged -= OnSessionMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnSessionPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnSessionTimelinePropertiesChanged;
        }
        catch
        {
            // Ignore if underlying WinRT object already finalized
        }

        try
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshLock.Dispose();
        }
        catch
        {
        }
    }
}
