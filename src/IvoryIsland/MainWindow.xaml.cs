using System.Collections.ObjectModel;
using IvoryIsland.Interop;
using IvoryIsland.Models;
using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using IvoryIsland.Views;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;

namespace IvoryIsland;

/// <summary>
/// Main application window delivering a premium macOS-inspired frosted white glass experience
/// with functional traffic-light controls, Safari-style tab system, modern address capsule,
/// and smooth view transitions.
/// </summary>
public sealed partial class MainWindow : Window, IDisposable
{
    private const int DefaultWindowWidth = 1020;
    private const int DefaultWindowHeight = 680;

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly OverlappedPresenter _presenter;
    private readonly DispatcherQueue _dispatcherQueue;

    private readonly SettingsService _settingsService;
    private readonly ArtworkCache _artworkCache;
    private readonly MediaSourceSelector _sourceSelector;
    private readonly MediaSessionManager _mediaSessionManager;
    private readonly AudioVolumeService _audioVolumeService;
    private readonly AnimationService _animationService;
    private readonly GlassBackdropService _glassBackdropService;
    private readonly IslandViewModel _viewModel;

    private readonly FloatingIslandWindow _floatingIslandWindow;
    private readonly SystemTrayService _systemTrayService;

    private readonly ObservableCollection<TabItemModel> _tabs = new();
    private readonly List<TabItemModel> _tabHistory = new();
    private int _historyIndex = -1;
    private bool _isDisposed;
    private bool _isFullscreen;
    private OverlappedPresenterState _preFullscreenState = OverlappedPresenterState.Restored;

    public MainWindow()
    {
        InitializeComponent();

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _presenter = (_appWindow.Presenter as OverlappedPresenter) ?? OverlappedPresenter.Create();

        // 1. Configure Window Chrome & Remove Duplicate Native Caption Buttons
        ConfigureWindowFrame();

        // 2. Initialize Core Services & Settings
        _settingsService = new SettingsService();
        _settingsService.Load();

        _artworkCache = new ArtworkCache(_dispatcherQueue, _settingsService);
        _sourceSelector = new MediaSourceSelector
        {
            SelectionMode = _settingsService.Current.SelectionMode
        };
        _mediaSessionManager = new MediaSessionManager(_dispatcherQueue, _artworkCache, _sourceSelector);
        _audioVolumeService = new AudioVolumeService();
        _audioVolumeService.Initialize();

        _animationService = new AnimationService();
        _viewModel = new IslandViewModel(
            _dispatcherQueue,
            _mediaSessionManager,
            _audioVolumeService,
            _settingsService);

        // 3. Configure Glass Backdrop for Main Window
        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(_settingsService.Current.GlassOpacity);

        // 4. Initialize Companion Floating Island Controller (Reference Image 2)
        _floatingIslandWindow = new FloatingIslandWindow(
            _viewModel,
            _settingsService,
            _animationService);

        // 5. Initialize System Tray Integration
        _systemTrayService = new SystemTrayService(_floatingIslandWindow.WindowManager, _settingsService);
        _systemTrayService.Initialize();
        _systemTrayService.ToggleIslandRequested += (_, _) => _dispatcherQueue.TryEnqueue(() => _floatingIslandWindow.ToggleIsland());
        _systemTrayService.ResetPositionRequested += (_, _) => _dispatcherQueue.TryEnqueue(() => _floatingIslandWindow.WindowManager.ResetToTopCenter());
        _systemTrayService.OpenSettingsRequested += (_, _) => _dispatcherQueue.TryEnqueue(() =>
        {
            Activate();
            SelectTabById("settings");
        });
        _systemTrayService.ExitApplicationRequested += (_, _) => _dispatcherQueue.TryEnqueue(CloseAndExit);

        // 6. Bind Tab Views
        NowPlayingView.Bind(_viewModel);
        AudioControlView.Bind(_viewModel);
        MediaSourcesView.Bind(_viewModel, _settingsService, _mediaSessionManager);
        SettingsView.Bind(_settingsService, _floatingIslandWindow.WindowManager, _glassBackdropService, _viewModel, _hwnd);

        // 7. Initialize macOS Safari-Inspired Tabs
        InitializeTabs();

        // 8. Wire Window & Navigation Events
        TrafficLights.CloseRequested += (_, _) => Close();
        TrafficLights.MinimizeRequested += (_, _) => _presenter.Minimize();
        TrafficLights.MaximizeRequested += (_, _) => ToggleMaximizeRestore();

        MainTabStrip.TabSelected += (_, tab) => SelectTab(tab);
        MainTabStrip.TabClosed += (_, tab) => CloseTab(tab);
        MainTabStrip.NewTabClicked += (_, _) => OnNewTabAction();

        NavRow.BackClicked += (_, _) => NavigateBack();
        NavRow.ForwardClicked += (_, _) => NavigateForward();
        NavRow.RefreshClicked += async (_, _) => await _mediaSessionManager.ReconcileSessionsAsync(isSessionListChange: true);
        NavRow.ToggleFloatingControllerClicked += (_, _) => _floatingIslandWindow.ToggleIsland();
        NavRow.ToggleMuteClicked += (_, _) => _viewModel.ToggleMute();

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IslandViewModel.IsMuted) or nameof(IslandViewModel.VolumeGlyph))
            {
                NavRow.SetMuteState(_viewModel.IsMuted, _viewModel.VolumeGlyph);
            }
        };

        RootWindowGrid.KeyDown += OnRootKeyDown;
        Closed += OnWindowClosed;

        // 9. Position and show window
        CenterAndSizeWindow();

        // 10. Start media discovery
        _ = _mediaSessionManager.InitializeAsync();
    }

    private void ConfigureWindowFrame()
    {
        // Hide native caption buttons completely by setting transparent titlebar colors and collapsed height
        _appWindow.Title = "Ivory Island";
        _appWindow.IsShownInSwitchers = true;

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverForegroundColor = Colors.Transparent;
            titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            titleBar.ButtonPressedForegroundColor = Colors.Transparent;
        }

        _presenter.IsResizable = true;
        _presenter.IsMaximizable = true;
        _presenter.IsMinimizable = true;

        // Request modern rounded corners on Windows 11
        int cornerPref = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(
            _hwnd,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref cornerPref,
            sizeof(int));
    }

    private void CenterAndSizeWindow()
    {
        DisplayArea display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        double scale = display.WorkArea.Width > 0 ? 1.0 : 1.0;
        uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
        if (dpi > 0) scale = dpi / 96.0;

        int width = (int)Math.Round(DefaultWindowWidth * scale);
        int height = (int)Math.Round(DefaultWindowHeight * scale);

        int x = display.WorkArea.X + (display.WorkArea.Width - width) / 2;
        int y = display.WorkArea.Y + (display.WorkArea.Height - height) / 2;

        _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
    }

    private void InitializeTabs()
    {
        _tabs.Add(new TabItemModel
        {
            Id = "now-playing",
            Title = "Now Playing",
            IconGlyph = "\uE8D6",
            IsActive = true,
            CanClose = true,
            NavigationUri = "ivory://now-playing"
        });

        _tabs.Add(new TabItemModel
        {
            Id = "audio-control",
            Title = "Audio Control",
            IconGlyph = "\uE995",
            IsActive = false,
            CanClose = true,
            NavigationUri = "ivory://audio-control"
        });

        _tabs.Add(new TabItemModel
        {
            Id = "media-sources",
            Title = "Media Sources",
            IconGlyph = "\uEC4F",
            IsActive = false,
            CanClose = true,
            NavigationUri = "ivory://media-sources"
        });

        _tabs.Add(new TabItemModel
        {
            Id = "settings",
            Title = "Settings",
            IconGlyph = "\uE713",
            IsActive = false,
            CanClose = true,
            NavigationUri = "ivory://settings"
        });

        MainTabStrip.BindTabs(_tabs);
        SelectTab(_tabs[0]);
    }

    public void SelectTabById(string tabId)
    {
        var existing = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (existing is not null)
        {
            SelectTab(existing);
        }
        else
        {
            var tab = CreateTabModelById(tabId);
            _tabs.Add(tab);
            SelectTab(tab);
        }
    }

    public void SelectTab(TabItemModel targetTab)
    {
        foreach (var tab in _tabs)
        {
            tab.IsActive = (tab == targetTab);
        }

        // Switch visible view
        NowPlayingView.Visibility = targetTab.Id == "now-playing" ? Visibility.Visible : Visibility.Collapsed;
        AudioControlView.Visibility = targetTab.Id == "audio-control" ? Visibility.Visible : Visibility.Collapsed;
        MediaSourcesView.Visibility = targetTab.Id == "media-sources" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = targetTab.Id == "settings" ? Visibility.Visible : Visibility.Collapsed;

        // Update address capsule
        NavRow.SetAddress(targetTab.NavigationUri);

        // Update navigation history
        if (_historyIndex == -1 || _historyIndex >= _tabHistory.Count || _tabHistory[_historyIndex] != targetTab)
        {
            while (_tabHistory.Count > _historyIndex + 1)
            {
                _tabHistory.RemoveAt(_tabHistory.Count - 1);
            }
            _tabHistory.Add(targetTab);
            _historyIndex = _tabHistory.Count - 1;
        }

        UpdateNavButtons();
    }

    public void CloseTab(TabItemModel tab)
    {
        if (_tabs.Count <= 1)
        {
            // Do not close final tab; ensure it remains active
            tab.IsActive = true;
            return;
        }

        int index = _tabs.IndexOf(tab);
        bool wasActive = tab.IsActive;

        _tabs.Remove(tab);

        if (wasActive)
        {
            int nextIndex = Math.Clamp(index, 0, _tabs.Count - 1);
            SelectTab(_tabs[nextIndex]);
        }
    }

    private void OnNewTabAction()
    {
        string[] allIds = ["now-playing", "audio-control", "media-sources", "settings"];
        string? missingId = allIds.FirstOrDefault(id => !_tabs.Any(t => t.Id == id));

        if (missingId is not null)
        {
            var newTab = CreateTabModelById(missingId);
            _tabs.Add(newTab);
            SelectTab(newTab);
        }
        else
        {
            // If all 4 default tabs are open, cycle to next tab
            int current = _tabs.IndexOf(_tabs.First(t => t.IsActive));
            int next = (current + 1) % _tabs.Count;
            SelectTab(_tabs[next]);
        }
    }

    private static TabItemModel CreateTabModelById(string id) => id switch
    {
        "audio-control" => new TabItemModel { Id = "audio-control", Title = "Audio Control", IconGlyph = "\uE995", NavigationUri = "ivory://audio-control" },
        "media-sources" => new TabItemModel { Id = "media-sources", Title = "Media Sources", IconGlyph = "\uEC4F", NavigationUri = "ivory://media-sources" },
        "settings" => new TabItemModel { Id = "settings", Title = "Settings", IconGlyph = "\uE713", NavigationUri = "ivory://settings" },
        _ => new TabItemModel { Id = "now-playing", Title = "Now Playing", IconGlyph = "\uE8D6", NavigationUri = "ivory://now-playing" }
    };

    private void NavigateBack()
    {
        if (_historyIndex > 0)
        {
            _historyIndex--;
            var target = _tabHistory[_historyIndex];
            if (!_tabs.Contains(target)) _tabs.Add(target);
            SelectTab(target);
        }
    }

    private void NavigateForward()
    {
        if (_historyIndex >= 0 && _historyIndex < _tabHistory.Count - 1)
        {
            _historyIndex++;
            var target = _tabHistory[_historyIndex];
            if (!_tabs.Contains(target)) _tabs.Add(target);
            SelectTab(target);
        }
    }

    private void UpdateNavButtons()
    {
        bool canBack = _historyIndex > 0;
        bool canForward = _historyIndex >= 0 && _historyIndex < _tabHistory.Count - 1;
        NavRow.SetNavigationState(canBack, canForward);
    }

    private void ToggleMaximizeRestore()
    {
        if (_isFullscreen)
        {
            ExitFullscreen();
            return;
        }

        if (_presenter.State == OverlappedPresenterState.Maximized)
        {
            _presenter.Restore();
        }
        else
        {
            _presenter.Maximize();
        }
    }

    public void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        _preFullscreenState = _presenter.State;
        _isFullscreen = true;
        _presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        _presenter.Maximize();
        WindowFrameBorder.CornerRadius = new CornerRadius(0);
    }

    private void ExitFullscreen()
    {
        _isFullscreen = false;
        _presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        if (_preFullscreenState == OverlappedPresenterState.Maximized)
        {
            _presenter.Maximize();
        }
        else
        {
            _presenter.Restore();
        }
        WindowFrameBorder.CornerRadius = new CornerRadius(16);
    }

    private void OnTitleBarPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(CustomTitleBarGrid).Properties.IsLeftButtonPressed)
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(
                _hwnd,
                NativeMethods.WM_SYSCOMMAND,
                (IntPtr)(NativeMethods.SC_MOVE + NativeMethods.HTCAPTION),
                IntPtr.Zero);
        }
    }

    private void OnTitleBarDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape && _isFullscreen)
        {
            ExitFullscreen();
            e.Handled = true;
        }
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
        _floatingIslandWindow.Dispose();
        _systemTrayService.Dispose();
        _mediaSessionManager.Dispose();
        _audioVolumeService.Dispose();
        _artworkCache.Dispose();
        _glassBackdropService.Dispose();
    }
}