using System.Runtime.CompilerServices;
using IvoryIsland.Models;
using Microsoft.UI.Dispatching;
using Windows.Media.Control;

namespace IvoryIsland.Services;

/// <summary>
/// Event-driven coordinator over Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.
/// Discovers all active media sessions (Spotify, Chrome, Edge, VLC, Apple Music, etc.), maintains
/// per-session controllers, and routes playback commands strictly to the currently selected session.
/// </summary>
public sealed class MediaSessionManager : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ArtworkCache _artworkCache;
    private readonly MediaSourceSelector _selector;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    private GlobalSystemMediaTransportControlsSessionManager? _gsmtcManager;
    private readonly Dictionary<string, MediaSessionController> _controllers = new(StringComparer.Ordinal);
    private bool _isDisposed;

    public event EventHandler<MediaSessionStateChangedEventArgs>? StateChanged;

    public MediaSourceSelector Selector => _selector;

    public MediaSessionManager(
        DispatcherQueue dispatcherQueue,
        ArtworkCache artworkCache,
        MediaSourceSelector selector)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _artworkCache = artworkCache ?? throw new ArgumentNullException(nameof(artworkCache));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed || _gsmtcManager is not null)
            return;

        try
        {
            _gsmtcManager = await GlobalSystemMediaTransportControlsSessionManager
                .RequestAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            if (_gsmtcManager is null)
                return;

            _gsmtcManager.SessionsChanged += OnGsmtcSessionsChanged;
            _gsmtcManager.CurrentSessionChanged += OnGsmtcCurrentSessionChanged;

            await ReconcileSessionsAsync(isSessionListChange: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // GSMTC may be unavailable on stripped Windows Server SKU; fail gracefully without crashing.
        }
    }

    private void OnGsmtcSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        _ = ReconcileSessionsAsync(isSessionListChange: true);
    }

    private void OnGsmtcCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _ = ReconcileSessionsAsync(isSessionListChange: false);
    }

    public async Task ReconcileSessionsAsync(bool isSessionListChange)
    {
        if (_isDisposed || _gsmtcManager is null)
            return;

        await _syncGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isDisposed || _gsmtcManager is null)
                return;

            IReadOnlyList<GlobalSystemMediaTransportControlsSession> rawSessions;
            try
            {
                rawSessions = _gsmtcManager.GetSessions();
            }
            catch
            {
                rawSessions = Array.Empty<GlobalSystemMediaTransportControlsSession>();
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var newlyCreatedControllers = new List<MediaSessionController>();

            for (int i = 0; i < rawSessions.Count; i++)
            {
                var rawSession = rawSessions[i];
                if (rawSession is null)
                    continue;

                string sessionId = BuildStableSessionId(rawSession, i);
                seenIds.Add(sessionId);

                if (!_controllers.TryGetValue(sessionId, out var controller))
                {
                    controller = new MediaSessionController(sessionId, rawSession, _artworkCache);
                    controller.SnapshotUpdated += OnControllerSnapshotUpdated;
                    _controllers[sessionId] = controller;
                    newlyCreatedControllers.Add(controller);
                }
            }

            // Remove and dispose any disconnected sessions immediately to prevent memory/event leaks
            var removedIds = _controllers.Keys.Where(id => !seenIds.Contains(id)).ToList();
            foreach (string removedId in removedIds)
            {
                if (_controllers.Remove(removedId, out var deadController))
                {
                    deadController.SnapshotUpdated -= OnControllerSnapshotUpdated;
                    deadController.Dispose();
                }
            }

            // Fetch initial properties for newly discovered sessions in parallel
            if (newlyCreatedControllers.Count > 0)
            {
                await Task.WhenAll(newlyCreatedControllers.Select(c => c.TriggerRefreshAsync(includeMediaProperties: true)))
                    .ConfigureAwait(false);
            }

            PublishCurrentState(isSessionListChange || removedIds.Count > 0 || newlyCreatedControllers.Count > 0);
        }
        finally
        {
            if (!_isDisposed)
            {
                try { _syncGate.Release(); } catch { }
            }
        }
    }

    private void OnControllerSnapshotUpdated(object? sender, MediaSessionSnapshot e)
    {
        if (_isDisposed)
            return;

        // Metadata or playback state updated on an existing session:
        // Never treat metadata updates as a session list change so the user's selected source never flickers.
        PublishCurrentState(isSessionListChange: false);
    }

    private void PublishCurrentState(bool isSessionListChange)
    {
        if (_isDisposed)
            return;

        List<MediaSessionSnapshot> snapshots;
        string? windowsCurrentId = null;

        lock (_controllers)
        {
            snapshots = _controllers.Values
                .Select(c => c.LatestSnapshot)
                .OrderByDescending(s => s.Identity.Kind == MediaSourceKind.Spotify)
                .ThenBy(s => s.Identity.ApplicationDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.SessionId, StringComparer.Ordinal)
                .ToList();
        }

        try
        {
            var winCurrent = _gsmtcManager?.GetCurrentSession();
            if (winCurrent is not null)
            {
                windowsCurrentId = MatchControllerId(winCurrent);
            }
        }
        catch
        {
        }

        var selected = _selector.EvaluateSelection(snapshots, windowsCurrentId, isSessionListChange);
        StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs(snapshots, selected));
    }

    public bool SelectSourceManually(string sessionId)
    {
        List<MediaSessionSnapshot> snapshots;
        lock (_controllers)
        {
            snapshots = _controllers.Values.Select(c => c.LatestSnapshot).ToList();
        }

        bool changed = _selector.SelectManually(sessionId, snapshots);
        if (changed)
        {
            PublishCurrentState(isSessionListChange: false);
        }
        return changed;
    }

    public Task<bool> TogglePlayPauseSelectedAsync()
    {
        var controller = GetSelectedController();
        return controller is not null ? controller.TryTogglePlayPauseAsync() : Task.FromResult(false);
    }

    public Task<bool> SkipNextSelectedAsync()
    {
        var controller = GetSelectedController();
        return controller is not null ? controller.TrySkipNextAsync() : Task.FromResult(false);
    }

    public Task<bool> SkipPreviousSelectedAsync()
    {
        var controller = GetSelectedController();
        return controller is not null ? controller.TrySkipPreviousAsync() : Task.FromResult(false);
    }

    public Task<bool> ToggleShuffleSelectedAsync()
    {
        var controller = GetSelectedController();
        return controller is not null ? controller.TryToggleShuffleAsync() : Task.FromResult(false);
    }

    public Task<bool> CycleRepeatSelectedAsync()
    {
        var controller = GetSelectedController();
        return controller is not null ? controller.TryCycleRepeatModeAsync() : Task.FromResult(false);
    }

    public Task<bool> SeekSelectedAsync(TimeSpan position)
    {
        var controller = GetSelectedController();
        return controller is not null ? controller.TryChangePlaybackPositionAsync(position) : Task.FromResult(false);
    }

    private MediaSessionController? GetSelectedController()
    {
        string? selectedId = _selector.SelectedSessionId;
        if (string.IsNullOrWhiteSpace(selectedId))
            return null;

        lock (_controllers)
        {
            return _controllers.TryGetValue(selectedId, out var controller) ? controller : null;
        }
    }

    private string? MatchControllerId(GlobalSystemMediaTransportControlsSession target)
    {
        string aumid;
        try { aumid = target.SourceAppUserModelId ?? "UnknownApp"; }
        catch { return null; }

        int hash = RuntimeHelpers.GetHashCode(target);
        string candidateId = $"{aumid}#{hash:X8}";

        lock (_controllers)
        {
            if (_controllers.ContainsKey(candidateId))
                return candidateId;

            var fallback = _controllers.Values.FirstOrDefault(c =>
                string.Equals(c.SourceAppUserModelId, aumid, StringComparison.OrdinalIgnoreCase));
            return fallback?.SessionId;
        }
    }

    private static string BuildStableSessionId(GlobalSystemMediaTransportControlsSession session, int indexFallback)
    {
        string aumid;
        try
        {
            aumid = session.SourceAppUserModelId ?? $"Session_{indexFallback}";
        }
        catch
        {
            aumid = $"Session_{indexFallback}";
        }

        int runtimeHash = RuntimeHelpers.GetHashCode(session);
        return $"{aumid}#{runtimeHash:X8}";
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        if (_gsmtcManager is not null)
        {
            try
            {
                _gsmtcManager.SessionsChanged -= OnGsmtcSessionsChanged;
                _gsmtcManager.CurrentSessionChanged -= OnGsmtcCurrentSessionChanged;
            }
            catch
            {
            }
        }

        lock (_controllers)
        {
            foreach (var controller in _controllers.Values)
            {
                controller.SnapshotUpdated -= OnControllerSnapshotUpdated;
                controller.Dispose();
            }
            _controllers.Clear();
        }

        try { _syncGate.Dispose(); } catch { }
    }
}

public sealed class MediaSessionStateChangedEventArgs : EventArgs
{
    public IReadOnlyList<MediaSessionSnapshot> AvailableSessions { get; }
    public MediaSessionSnapshot? SelectedSession { get; }

    public MediaSessionStateChangedEventArgs(
        IReadOnlyList<MediaSessionSnapshot> availableSessions,
        MediaSessionSnapshot? selectedSession)
    {
        AvailableSessions = availableSessions;
        SelectedSession = selectedSession;
    }
}
