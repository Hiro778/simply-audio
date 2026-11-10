using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using IvoryIsland.Models;
using IvoryIsland.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

namespace IvoryIsland.ViewModels;

/// <summary>
/// Primary ViewModel for the unified Ivory Island floating media capsule.
/// Marshals all Windows GSMTC media session updates and WASAPI volume notifications onto the UI
/// DispatcherQueue, prevents metadata mixing across sessions, and manages adaptive island states
/// with zero idle polling.
/// </summary>
public sealed class IslandViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly MediaSessionManager _mediaManager;
    private readonly AudioVolumeService _volumeService;
    private readonly SettingsService _settingsService;
    private readonly DispatcherQueueTimer _transientVolumeTimer;

    private MediaSessionSnapshot? _selectedSession;
    private IslandVisualState _visualState = IslandVisualState.IdleCompact;
    private double _volumePercent = 70.0;
    private bool _isMuted;
    private bool _suppressVolumeWriteback;
    private bool _isSourceDrawerOpen;
    private bool _isOptionsDrawerOpen;
    private string? _lastAnimatedSessionId;
    private bool _isDisposed;

    public ObservableCollection<MediaSessionSnapshot> AvailableSessions { get; } = new();

    public MediaSessionSnapshot? SelectedSession
    {
        get => _selectedSession;
        private set
        {
            if (!ReferenceEquals(_selectedSession, value))
            {
                string? previousId = _selectedSession?.SessionId;
                _selectedSession = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveSession));
                OnPropertyChanged(nameof(TrackTitle));
                OnPropertyChanged(nameof(ArtistName));
                OnPropertyChanged(nameof(SourceDisplayName));
                OnPropertyChanged(nameof(AlbumArtwork));
                OnPropertyChanged(nameof(HasAlbumArtwork));
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(PlayPauseGlyph));
                OnPropertyChanged(nameof(CanTogglePlayPause));
                OnPropertyChanged(nameof(CanSkipNext));
                OnPropertyChanged(nameof(CanSkipPrevious));
                OnPropertyChanged(nameof(CanShuffle));
                OnPropertyChanged(nameof(CanRepeat));
                OnPropertyChanged(nameof(IsShuffleActive));
                OnPropertyChanged(nameof(RepeatMode));
                OnPropertyChanged(nameof(IsSelectedSourceSpotify));
                OnPropertyChanged(nameof(IsSelectedSourceBrowser));

                if (!string.Equals(previousId, value?.SessionId, StringComparison.Ordinal))
                {
                    _lastAnimatedSessionId = value?.SessionId;
                    SourceSwitched?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    public IslandVisualState VisualState
    {
        get => _visualState;
        private set
        {
            if (_visualState != value)
            {
                var prev = _visualState;
                _visualState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFullMediaCapsuleVisible));
                OnPropertyChanged(nameof(IsVolumeOnlyCompactMode));
                OnPropertyChanged(nameof(IsCapsuleCollapsedOrHidden));
                VisualStateChanged?.Invoke(this, (prev, value));
            }
        }
    }

    public bool HasActiveSession => _selectedSession is not null;
    public int SessionCount => AvailableSessions.Count;
    public bool HasMultipleSessions => AvailableSessions.Count > 1;
    public string SessionCountBadgeText => AvailableSessions.Count > 1 ? AvailableSessions.Count.ToString() : string.Empty;

    public bool HasSpotifySession => AvailableSessions.Any(s => s.Identity.Kind == MediaSourceKind.Spotify);
    public bool HasBrowserSession => AvailableSessions.Any(s => s.Identity.IsBrowserSession);
    public MediaSessionSnapshot? PrimarySpotifySession => AvailableSessions.FirstOrDefault(s => s.Identity.Kind == MediaSourceKind.Spotify);
    public MediaSessionSnapshot? PrimaryBrowserSession => AvailableSessions.FirstOrDefault(s => s.Identity.IsBrowserSession);

    public bool IsSelectedSourceSpotify => _selectedSession?.Identity.Kind == MediaSourceKind.Spotify;
    public bool IsSelectedSourceBrowser => _selectedSession?.Identity.IsBrowserSession == true;

    public string TrackTitle => _selectedSession?.DisplayTitle ?? "No Active Media";
    public string ArtistName => _selectedSession?.DisplaySubtitle ?? "Play music in Spotify or browser";
    public string SourceDisplayName => _selectedSession?.Identity.ApplicationDisplayName ?? "System Audio";
    public BitmapImage? AlbumArtwork => _selectedSession?.ArtworkImage;
    public bool HasAlbumArtwork => _selectedSession?.ArtworkImage is not null;

    public bool IsPlaying => _selectedSession?.IsPlaying == true;
    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768"; // Pause vs Play Segoe Fluent Icons

    public bool CanTogglePlayPause => _selectedSession?.CanTogglePlayPause ?? false;
    public bool CanSkipNext => _selectedSession?.CanSkipNext ?? false;
    public bool CanSkipPrevious => _selectedSession?.CanSkipPrevious ?? false;
    public bool CanShuffle => _selectedSession?.CanShuffle ?? false;
    public bool CanRepeat => _selectedSession?.CanRepeat ?? false;
    public bool IsShuffleActive => _selectedSession?.IsShuffleActive ?? false;
    public string RepeatMode => _selectedSession?.RepeatMode ?? "None";

    public double VolumePercent
    {
        get => _volumePercent;
        set
        {
            double clamped = Math.Clamp(Math.Round(value), 0.0, 100.0);
            if (Math.Abs(_volumePercent - clamped) > 0.01)
            {
                _volumePercent = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumePercentageText));
                OnPropertyChanged(nameof(VolumeGlyph));

                if (!_suppressVolumeWriteback)
                {
                    _volumeService.SetVolumePercent(clamped);
                }
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumeGlyph));
                OnPropertyChanged(nameof(VolumePercentageText));
            }
        }
    }

    public string VolumePercentageText => _isMuted ? "Muted" : $"{(int)Math.Round(_volumePercent)}%";

    public string VolumeGlyph
    {
        get
        {
            if (_isMuted || _volumePercent <= 0.5)
                return "\uE74F"; // Mute
            if (_volumePercent < 33.0)
                return "\uE993"; // Volume 1
            if (_volumePercent < 66.0)
                return "\uE994"; // Volume 2
            return "\uE995";     // Volume 3
        }
    }

    public bool IsSourceDrawerOpen
    {
        get => _isSourceDrawerOpen;
        set
        {
            if (_isSourceDrawerOpen != value)
            {
                _isSourceDrawerOpen = value;
                if (value) _isOptionsDrawerOpen = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOptionsDrawerOpen));
                DrawerStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsOptionsDrawerOpen
    {
        get => _isOptionsDrawerOpen;
        set
        {
            if (_isOptionsDrawerOpen != value)
            {
                _isOptionsDrawerOpen = value;
                if (value) _isSourceDrawerOpen = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSourceDrawerOpen));
                DrawerStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsFullMediaCapsuleVisible =>
        _visualState is IslandVisualState.MediaPlaying or IslandVisualState.MediaPaused or IslandVisualState.IdleCompact;

    public bool IsVolumeOnlyCompactMode =>
        _visualState == IslandVisualState.VolumeOnlyTransient;

    public bool IsCapsuleCollapsedOrHidden =>
        _visualState == IslandVisualState.IdleHidden;

    public AppSettings Settings => _settingsService.Current;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SourceSwitched;
    public event EventHandler<(IslandVisualState Previous, IslandVisualState Current)>? VisualStateChanged;
    public event EventHandler? DrawerStateChanged;

    public IslandViewModel(
        DispatcherQueue dispatcherQueue,
        MediaSessionManager mediaManager,
        AudioVolumeService volumeService,
        SettingsService settingsService)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _mediaManager = mediaManager ?? throw new ArgumentNullException(nameof(mediaManager));
        _volumeService = volumeService ?? throw new ArgumentNullException(nameof(volumeService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _transientVolumeTimer = _dispatcherQueue.CreateTimer();
        _transientVolumeTimer.Interval = TimeSpan.FromMilliseconds(2200);
        _transientVolumeTimer.IsRepeating = false;
        _transientVolumeTimer.Tick += OnTransientVolumeTimerTick;

        _mediaManager.StateChanged += OnMediaManagerStateChanged;
        _volumeService.VolumeChanged += OnVolumeServiceVolumeChanged;

        _suppressVolumeWriteback = true;
        _volumePercent = _volumeService.CurrentVolumePercent;
        _isMuted = _volumeService.IsMuted;
        _suppressVolumeWriteback = false;
    }

    private void OnMediaManagerStateChanged(object? sender, MediaSessionStateChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_isDisposed)
                return;

            AvailableSessions.Clear();
            foreach (var session in e.AvailableSessions)
            {
                AvailableSessions.Add(session);
            }

            SelectedSession = e.SelectedSession;

            OnPropertyChanged(nameof(SessionCount));
            OnPropertyChanged(nameof(HasMultipleSessions));
            OnPropertyChanged(nameof(SessionCountBadgeText));
            OnPropertyChanged(nameof(HasSpotifySession));
            OnPropertyChanged(nameof(HasBrowserSession));
            OnPropertyChanged(nameof(PrimarySpotifySession));
            OnPropertyChanged(nameof(PrimaryBrowserSession));

            RecalculateVisualState(isExternalVolumeTrigger: false);
        });
    }

    private void OnVolumeServiceVolumeChanged(object? sender, AudioVolumeChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_isDisposed)
                return;

            _suppressVolumeWriteback = true;
            VolumePercent = e.VolumePercent;
            IsMuted = e.IsMuted;
            _suppressVolumeWriteback = false;

            if (e.IsExternalChange && _selectedSession is null)
            {
                RecalculateVisualState(isExternalVolumeTrigger: true);
            }
        });
    }

    private void OnTransientVolumeTimerTick(DispatcherQueueTimer sender, object args)
    {
        _transientVolumeTimer.Stop();
        if (_selectedSession is null)
        {
            RecalculateVisualState(isExternalVolumeTrigger: false);
        }
    }

    public void RecalculateVisualState(bool isExternalVolumeTrigger)
    {
        if (_selectedSession is not null)
        {
            _transientVolumeTimer.Stop();
            VisualState = _selectedSession.IsPlaying
                ? IslandVisualState.MediaPlaying
                : IslandVisualState.MediaPaused;
            return;
        }

        if (isExternalVolumeTrigger)
        {
            VisualState = IslandVisualState.VolumeOnlyTransient;
            _transientVolumeTimer.Stop();
            _transientVolumeTimer.Start();
            return;
        }

        VisualState = _settingsService.Current.UseCompactIdleMode
            ? IslandVisualState.IdleCompact
            : IslandVisualState.IdleHidden;
    }

    public void SelectSource(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        if (_mediaManager.SelectSourceManually(sessionId))
        {
            _settingsService.Current.LastSelectedSessionId = sessionId;
            _settingsService.Save();
            IsSourceDrawerOpen = false;
        }
    }

    public void SelectSpotifyIfAvailable()
    {
        var spotify = PrimarySpotifySession;
        if (spotify is not null)
            SelectSource(spotify.SessionId);
    }

    public void SelectBrowserIfAvailable()
    {
        var browser = PrimaryBrowserSession;
        if (browser is not null)
            SelectSource(browser.SessionId);
    }

    public Task TogglePlayPauseAsync() => _mediaManager.TogglePlayPauseSelectedAsync();
    public Task SkipNextAsync() => _mediaManager.SkipNextSelectedAsync();
    public Task SkipPreviousAsync() => _mediaManager.SkipPreviousSelectedAsync();
    public Task ToggleShuffleAsync() => _mediaManager.ToggleShuffleSelectedAsync();
    public Task CycleRepeatAsync() => _mediaManager.CycleRepeatSelectedAsync();
    public void ToggleMute() => _volumeService.ToggleMute();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _transientVolumeTimer.Stop();
        _mediaManager.StateChanged -= OnMediaManagerStateChanged;
        _volumeService.VolumeChanged -= OnVolumeServiceVolumeChanged;
    }
}
