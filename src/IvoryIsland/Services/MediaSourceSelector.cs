using IvoryIsland.Models;

namespace IvoryIsland.Services;

/// <summary>
/// Deterministic, anti-flicker session selection engine.
/// Enforces:
/// 1. Initial selection uses Windows GetCurrentSession() when available, otherwise a Playing session.
/// 2. User manual selection remains strictly stable until that session closes or user switches again.
/// 3. Never auto-switches between Spotify and YouTube when metadata changes.
/// 4. Falls back gracefully to the best remaining session when the selected session disconnects.
/// </summary>
public sealed class MediaSourceSelector
{
    private readonly object _lock = new();
    private string? _selectedSessionId;
    private bool _isManuallyPinned;
    private SourceSelectionMode _selectionMode = SourceSelectionMode.SmartStable;

    public string? SelectedSessionId
    {
        get { lock (_lock) return _selectedSessionId; }
    }

    public bool IsManuallyPinned
    {
        get { lock (_lock) return _isManuallyPinned; }
    }

    public SourceSelectionMode SelectionMode
    {
        get { lock (_lock) return _selectionMode; }
        set { lock (_lock) _selectionMode = value; }
    }

    /// <summary>
    /// Explicitly selects a media session in response to a user clicking a source icon or dropdown item.
    /// Marks the selection as manually pinned so background events never override it while that session exists.
    /// </summary>
    public bool SelectManually(string sessionId, IReadOnlyList<MediaSessionSnapshot> availableSessions)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        lock (_lock)
        {
            bool exists = availableSessions.Any(s => string.Equals(s.SessionId, sessionId, StringComparison.Ordinal));
            if (!exists)
                return false;

            _selectedSessionId = sessionId;
            _isManuallyPinned = true;
            return true;
        }
    }

    /// <summary>
    /// Evaluates the active session list and returns the authoritative selected session snapshot.
    /// Never switches away from an existing valid selection merely because another session updated its metadata.
    /// </summary>
    public MediaSessionSnapshot? EvaluateSelection(
        IReadOnlyList<MediaSessionSnapshot> sessions,
        string? windowsCurrentSessionId,
        bool isInitialOrSessionListChanged = false)
    {
        lock (_lock)
        {
            if (sessions is null || sessions.Count == 0)
            {
                _selectedSessionId = null;
                _isManuallyPinned = false;
                return null;
            }

            // 1. Check if our currently selected session still exists in the active list
            var currentSelected = _selectedSessionId is not null
                ? sessions.FirstOrDefault(s => string.Equals(s.SessionId, _selectedSessionId, StringComparison.Ordinal))
                : null;

            if (currentSelected is not null)
            {
                // Keep manual selection unconditionally stable while the session remains alive.
                if (_isManuallyPinned || _selectionMode == SourceSelectionMode.ManualLocked)
                {
                    return currentSelected;
                }

                // In SmartStable mode, never switch away from a playing session, and never switch on metadata updates.
                // Only if the session list itself changed or Windows current session explicitly started playing while
                // our current session is paused/stopped do we consider updating.
                if (currentSelected.IsPlaying || !isInitialOrSessionListChanged)
                {
                    return currentSelected;
                }
            }
            else
            {
                // The previously selected session closed; clear manual pin and pick the best remaining session.
                _isManuallyPinned = false;
            }

            // 2. If selecting initially or after the selected session closed:
            // Prefer a currently playing session that matches Windows CurrentSession first
            if (!string.IsNullOrWhiteSpace(windowsCurrentSessionId))
            {
                var winCurrentPlaying = sessions.FirstOrDefault(s =>
                    string.Equals(s.SessionId, windowsCurrentSessionId, StringComparison.Ordinal) && s.IsPlaying);
                if (winCurrentPlaying is not null)
                {
                    _selectedSessionId = winCurrentPlaying.SessionId;
                    return winCurrentPlaying;
                }
            }

            // 3. Otherwise prefer any currently Playing session
            var anyPlaying = sessions.FirstOrDefault(s => s.IsPlaying);
            if (anyPlaying is not null)
            {
                _selectedSessionId = anyPlaying.SessionId;
                return anyPlaying;
            }

            // 4. Otherwise prefer Windows GetCurrentSession() even if paused
            if (!string.IsNullOrWhiteSpace(windowsCurrentSessionId))
            {
                var winCurrent = sessions.FirstOrDefault(s =>
                    string.Equals(s.SessionId, windowsCurrentSessionId, StringComparison.Ordinal));
                if (winCurrent is not null)
                {
                    _selectedSessionId = winCurrent.SessionId;
                    return winCurrent;
                }
            }

            // 5. Fallback to the first available session
            var fallback = sessions[0];
            _selectedSessionId = fallback.SessionId;
            return fallback;
        }
    }
}
