using IvoryIsland.Interop;
using IvoryIsland.Models;
using IvoryIsland.ViewModels;
using IvoryIsland.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace IvoryIsland.Services;

/// <summary>
/// Master coordinator for Ivory Desktop Full Transformation.
/// Orchestrates the global Menu Bar, Dock, App Launcher (Launchpad), Control Center,
/// Mission Control (Window Overview), Volume HUD, Floating Media Island, and Emergency Recovery.
/// </summary>
public sealed class DesktopHostService : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SettingsService _settingsService;
    private readonly SystemStatusService _statusService;
    private readonly AppDiscoveryService _discoveryService;
    private readonly WindowManagementService _windowService;
    private readonly EmergencyRecoveryService _recoveryService;
    private readonly IslandViewModel _viewModel;

    private readonly MenuBarWindow _menuBarWindow;
    private readonly DockWindow _dockWindow;
    private readonly LauncherWindow _launcherWindow;
    private readonly ControlCenterWindow _controlCenterWindow;
    private readonly VolumeHudWindow _volumeHudWindow;
    private readonly WindowOverviewWindow _overviewWindow;
    private readonly FloatingIslandWindow _floatingIslandWindow;
    private readonly MainWindow _mainWindow;

    private DispatcherQueueTimer? _runningAppsTimer;
    private bool _isDisposed;

    public MenuBarWindow MenuBar => _menuBarWindow;
    public DockWindow Dock => _dockWindow;
    public ControlCenterWindow ControlCenter => _controlCenterWindow;
    public LauncherWindow Launcher => _launcherWindow;
    public WindowOverviewWindow Overview => _overviewWindow;
    public VolumeHudWindow VolumeHud => _volumeHudWindow;
    public FloatingIslandWindow FloatingIsland => _floatingIslandWindow;
    public MainWindow MainWindow => _mainWindow;

    public DesktopHostService(
        DispatcherQueue dispatcherQueue,
        SettingsService settingsService,
        SystemStatusService statusService,
        AppDiscoveryService discoveryService,
        WindowManagementService windowService,
        EmergencyRecoveryService recoveryService,
        IslandViewModel viewModel,
        FloatingIslandWindow floatingIslandWindow,
        MainWindow mainWindow)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _recoveryService = recoveryService ?? throw new ArgumentNullException(nameof(recoveryService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _floatingIslandWindow = floatingIslandWindow ?? throw new ArgumentNullException(nameof(floatingIslandWindow));
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        // 1. Create Desktop Windows
        _menuBarWindow = new MenuBarWindow(_settingsService, _statusService, _viewModel);
        _dockWindow = new DockWindow(_settingsService);
        _launcherWindow = new LauncherWindow(_settingsService);
        _controlCenterWindow = new ControlCenterWindow(_settingsService, _statusService, _viewModel);
        _volumeHudWindow = new VolumeHudWindow(_settingsService);
        _overviewWindow = new WindowOverviewWindow(_settingsService);

        // 2. Register all Ivory windows with WindowManagementService to prevent self-enumeration
        _windowService.RegisterIgnoredWindow(_menuBarWindow.WindowHandle);
        _windowService.RegisterIgnoredWindow(_dockWindow.WindowHandle);
        _windowService.RegisterIgnoredWindow(_launcherWindow.WindowHandle);
        _windowService.RegisterIgnoredWindow(_controlCenterWindow.WindowHandle);
        _windowService.RegisterIgnoredWindow(_volumeHudWindow.WindowHandle);
        _windowService.RegisterIgnoredWindow(_overviewWindow.WindowHandle);
        _windowService.RegisterIgnoredWindow(_floatingIslandWindow.WindowManager.WindowHandle);
        _windowService.RegisterIgnoredWindow(WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow));

        // 3. Register Emergency Hotkey (Ctrl+Alt+Escape)
        _recoveryService.RegisterEmergencyHotkey(_menuBarWindow.WindowHandle);
        _recoveryService.EmergencyTriggered += OnEmergencyRecoveryTriggered;

        // 4. Wire Interactions
        WireEventHandlers();

        // 5. Populate initial apps & running windows
        _ = InitializeApplicationsAsync();

        // 6. Start low-overhead timer to refresh running apps on the Dock
        _runningAppsTimer = _dispatcherQueue.CreateTimer();
        _runningAppsTimer.Interval = TimeSpan.FromSeconds(3);
        _runningAppsTimer.Tick += (_, _) => RefreshRunningApps();
        _runningAppsTimer.Start();

        // 7. Apply initial visibility based on selected Desktop Operating Mode
        ApplyOperatingMode();
    }

    private void WireEventHandlers()
    {
        // Menu Bar events
        _menuBarWindow.LaunchpadRequested += (_, _) => _launcherWindow.ToggleLauncher();
        _menuBarWindow.MissionControlRequested += (_, _) => ToggleWindowOverview();
        _menuBarWindow.ControlCenterRequested += (_, _) => _controlCenterWindow.ToggleControlCenter();
        _menuBarWindow.ToggleIslandRequested += (_, _) => _floatingIslandWindow.ToggleIsland();
        _menuBarWindow.SettingsRequested += (_, _) => OpenSettingsWindow();
        _menuBarWindow.AboutRequested += (_, _) => OpenMainWindow();
        _menuBarWindow.ToggleModeRequested += (_, _) => ToggleOperatingMode();
        _menuBarWindow.RestartRequested += (_, _) => RestartApplication();
        _menuBarWindow.ExitRequested += (_, _) => ExitApplication();

        // Dock events
        _dockWindow.LaunchpadRequested += (_, _) => _launcherWindow.ToggleLauncher();
        _dockWindow.SettingsRequested += (_, _) => OpenSettingsWindow();
        _dockWindow.ToggleMediaRequested += (_, _) => _floatingIslandWindow.ToggleIsland();

        // Control Center events
        _controlCenterWindow.ToggleModeRequested += (_, _) => ToggleOperatingMode();
        _controlCenterWindow.ExitRequested += (_, _) => ExitApplication();

        // System status volume HUD trigger
        _statusService.StatusUpdated += (_, snapshot) =>
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                _volumeHudWindow.ShowVolume(snapshot.Volume, snapshot.IsMuted);
                _menuBarWindow.UpdateActiveAppTitle(WindowManagementService.GetActiveWindowTitle());
            });
        };
    }

    private async Task InitializeApplicationsAsync()
    {
        var apps = await _discoveryService.DiscoverApplicationsAsync();
        _dispatcherQueue.TryEnqueue(() =>
        {
            _launcherWindow.SetApplications(apps);
            RefreshRunningApps();
        });
    }

    private void RefreshRunningApps()
    {
        if (_isDisposed)
            return;

        try
        {
            var windows = _windowService.EnumerateApplicationWindows();
            _dockWindow.PopulateRunningApps(windows);
        }
        catch { }
    }

    public void ToggleWindowOverview()
    {
        var windows = _windowService.EnumerateApplicationWindows();
        _overviewWindow.ToggleOverview(windows);
    }

    public void OpenSettingsWindow()
    {
        _mainWindow.Activate();
        _mainWindow.SelectTabById("settings");
    }

    public void OpenMainWindow()
    {
        _mainWindow.Activate();
    }

    public void ToggleOperatingMode()
    {
        _settingsService.Current.OperatingMode = _settingsService.Current.OperatingMode == DesktopOperatingMode.Immersive
            ? DesktopOperatingMode.Standard
            : DesktopOperatingMode.Immersive;
        _settingsService.Save();

        ApplyOperatingMode();
    }

    private void ApplyOperatingMode()
    {
        bool isImmersive = _settingsService.Current.OperatingMode == DesktopOperatingMode.Immersive;

        _menuBarWindow.SetVisible(isImmersive);
        _dockWindow.SetVisible(isImmersive);

        if (isImmersive)
        {
            _menuBarWindow.Reposition();
            _dockWindow.Reposition();
        }
        else
        {
            _launcherWindow.HideLauncher();
            _controlCenterWindow.HideControlCenter();
            _overviewWindow.HideOverview();
        }
    }

    private void OnEmergencyRecoveryTriggered(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Guaranteed recovery: revert to Standard mode, ensure explorer is alive, show taskbar settings
            _settingsService.Current.OperatingMode = DesktopOperatingMode.Standard;
            _settingsService.Save();
            ApplyOperatingMode();

            EmergencyRecoveryService.EnsureExplorerRunning();
            EmergencyRecoveryService.LaunchTaskManager();
        });
    }

    public void RestartApplication()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
            }
        }
        catch { }
        ExitApplication();
    }

    public void ExitApplication()
    {
        Dispose();
        Application.Current.Exit();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        if (_runningAppsTimer is not null)
        {
            _runningAppsTimer.Stop();
            _runningAppsTimer = null;
        }

        _recoveryService.Dispose();
        _menuBarWindow.Dispose();
        _dockWindow.Dispose();
        _launcherWindow.Dispose();
        _controlCenterWindow.Dispose();
        _volumeHudWindow.Dispose();
        _overviewWindow.Dispose();
        _statusService.Dispose();
    }
}
