using IvoryIsland.Models;
using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace IvoryIsland;

/// <summary>
/// Native borderless topmost overlay window hosting the unified FloatingIslandView.
/// Coordinates lifecycle between WindowManager, GlassBackdropService, SystemTrayService,
/// MediaSessionManager, AudioVolumeService, and SettingsService.
/// </summary>
public sealed partial class MainWindow : Window, IDisposable
{
    private const int ExpandedLogicalWidth = 780;
    private const int CompactVolumeLogicalWidth = 340;
    private const int BaseLogicalHeight = 88;
    private const int DrawerOpenLogicalHeight = 312;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SettingsService _settingsService;
    private readonly ArtworkCache _artworkCache;
    private readonly MediaSourceSelector _sourceSelector;
    private readonly MediaSessionManager _mediaSessionManager;
    private readonly AudioVolumeService _audioVolumeService;
    private readonly WindowManager _windowManager;
    private readonly GlassBackdropService _glassBackdropService;
    private readonly SystemTrayService _systemTrayService;
    private readonly AnimationService _animationService;
    private readonly IslandViewModel _viewModel;
    private bool _isDisposed;

    public MainWindow()
    {
        InitializeComponent();

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // 1. Load user settings from %LOCALAPPDATA%\IvoryIsland (separated from binary directory)
        _settingsService = new SettingsService();
        _settingsService.Load();

        // 2. Initialize core services
        _artworkCache = new ArtworkCache(_dispatcherQueue, _settingsService);
        _sourceSelector = new MediaSourceSelector
        {
            SelectionMode = _settingsService.Current.SelectionMode
        };
        _mediaSessionManager = new MediaSessionManager(_dispatcherQueue, _artworkCache, _sourceSelector);
        _audioVolumeService = new AudioVolumeService();
        _audioVolumeService.Initialize();

        // 3. Configure native Win32 topmost non-activating overlay & backdrop
        _windowManager = new WindowManager(this, _settingsService);
        _windowManager.InitializeOverlayWindow();

        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(_settingsService.Current.GlassOpacity);

        // 4. Initialize System Tray
        _systemTrayService = new SystemTrayService(_windowManager, _settingsService);
        _systemTrayService.Initialize();

        // 5. Initialize Animation Service and ViewModel
        _animationService = new AnimationService();
        _viewModel = new IslandViewModel(
            _dispatcherQueue,
            _mediaSessionManager,
            _audioVolumeService,
            _settingsService);

        IslandView.Bind(_viewModel, _windowManager, _animationService, _settingsService, _glassBackdropService);

        // 6. Wire events
        _viewModel.VisualStateChanged += OnViewModelVisualStateChanged;
        _viewModel.DrawerStateChanged += OnViewModelDrawerStateChanged;
        _windowManager.SystemResumeOrUnlockDetected += OnSystemResumeOrUnlockDetected;

        _systemTrayService.ToggleIslandRequested += OnTrayToggleIslandRequested;
        _systemTrayService.ResetPositionRequested += (_, _) => _dispatcherQueue.TryEnqueue(() => _windowManager.ResetToTopCenter());
        _systemTrayService.OpenSettingsRequested += (_, _) => _dispatcherQueue.TryEnqueue(() =>
        {
            _settingsService.Current.IsIslandEnabled = true;
            _windowManager.SetOverlayVisible(true);
            _viewModel.IsOptionsDrawerOpen = true;
        });
        _systemTrayService.ExitApplicationRequested += (_, _) => _dispatcherQueue.TryEnqueue(CloseAndExit);

        Closed += OnWindowClosed;

        // 7. Start asynchronous Windows GSMTC session discovery without blocking startup
        _ = _mediaSessionManager.InitializeAsync();
    }

    private void OnViewModelVisualStateChanged(object? sender, (IslandVisualState Previous, IslandVisualState Current) e)
    {
        UpdateWindowGeometryForState();
    }

    private void OnViewModelDrawerStateChanged(object? sender, EventArgs e)
    {
        UpdateWindowGeometryForState();
    }

    private void UpdateWindowGeometryForState()
    {
        if (_isDisposed)
            return;

        if (!_settingsService.Current.IsIslandEnabled)
        {
            _windowManager.SetOverlayVisible(false);
            return;
        }

        bool isDrawerOpen = _viewModel.IsSourceDrawerOpen || _viewModel.IsOptionsDrawerOpen;
        int targetHeight = isDrawerOpen ? DrawerOpenLogicalHeight : BaseLogicalHeight;

        switch (_viewModel.VisualState)
        {
            case IslandVisualState.IdleHidden:
                if (!isDrawerOpen)
                {
                    _windowManager.SetOverlayVisible(false);
                    return;
                }
                _windowManager.SetOverlayVisible(true);
                _windowManager.UpdateSizeAndPosition(ExpandedLogicalWidth, targetHeight);
                break;

            case IslandVisualState.VolumeOnlyTransient:
                _windowManager.SetOverlayVisible(true);
                _windowManager.UpdateSizeAndPosition(
                    isDrawerOpen ? ExpandedLogicalWidth : CompactVolumeLogicalWidth,
                    targetHeight);
                break;

            default:
                _windowManager.SetOverlayVisible(true);
                _windowManager.UpdateSizeAndPosition(ExpandedLogicalWidth, targetHeight);
                break;
        }
    }

    private void OnSystemResumeOrUnlockDetected(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(async () =>
        {
            await _mediaSessionManager.ReconcileSessionsAsync(isSessionListChange: true);
            _windowManager.EvaluateFullscreenState();
            UpdateWindowGeometryForState();
        });
    }

    private void OnTrayToggleIslandRequested(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            bool nextState = !_settingsService.Current.IsIslandEnabled;
            _settingsService.Current.IsIslandEnabled = nextState;
            _settingsService.Save();
            UpdateWindowGeometryForState();
        });
    }

    private void CloseAndExit()
    {
        Dispose();
        Application.Current.Exit();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _viewModel.Dispose();
        _systemTrayService.Dispose();
        _mediaSessionManager.Dispose();
        _audioVolumeService.Dispose();
        _artworkCache.Dispose();
        _glassBackdropService.Dispose();
        _windowManager.Dispose();
    }
}
