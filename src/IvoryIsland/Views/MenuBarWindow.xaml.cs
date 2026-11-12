using IvoryIsland.Interop;
using IvoryIsland.Models;
using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace IvoryIsland.Views;

public sealed partial class MenuBarWindow : Window, IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly SettingsService _settingsService;
    private readonly SystemStatusService _statusService;
    private readonly IslandViewModel _viewModel;
    private readonly GlassBackdropService _glassBackdropService;
    private bool _isDisposed;

    public IntPtr WindowHandle => _hwnd;

    public event EventHandler? AboutRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? LaunchpadRequested;
    public event EventHandler? MissionControlRequested;
    public event EventHandler? ToggleIslandRequested;
    public event EventHandler? ToggleModeRequested;
    public event EventHandler? ControlCenterRequested;
    public event EventHandler? RestartRequested;
    public event EventHandler? ExitRequested;

    public MenuBarWindow(
        SettingsService settingsService,
        SystemStatusService statusService,
        IslandViewModel viewModel)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        DesktopWindowHelper.ConfigureOverlayWindow(this, out _hwnd, out _appWindow, roundCorners: false);

        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(_settingsService.Current.GlassOpacity);

        WireEvents();
        UpdateStatus(_statusService.Current);
        Reposition();
    }

    private void WireEvents()
    {
        MenuAbout.Click += (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty);
        MenuSettings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        MenuLaunchpad.Click += (_, _) => LaunchpadRequested?.Invoke(this, EventArgs.Empty);
        MenuMissionControl.Click += (_, _) => MissionControlRequested?.Invoke(this, EventArgs.Empty);
        MenuToggleIsland.Click += (_, _) => ToggleIslandRequested?.Invoke(this, EventArgs.Empty);
        MenuTaskManager.Click += (_, _) => EmergencyRecoveryService.LaunchTaskManager();
        MenuExplorer.Click += (_, _) => EmergencyRecoveryService.EnsureExplorerRunning();
        MenuTaskbarSettings.Click += (_, _) => _statusService.OpenTaskbarSettings();
        MenuToggleMode.Click += (_, _) => ToggleModeRequested?.Invoke(this, EventArgs.Empty);
        MenuRestart.Click += (_, _) => RestartRequested?.Invoke(this, EventArgs.Empty);
        MenuExit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        ToggleMediaIslandButton.Click += (_, _) => ToggleIslandRequested?.Invoke(this, EventArgs.Empty);
        WifiStatusButton.Click += (_, _) => _statusService.OpenWifiSettings();
        BatteryStatusButton.Click += (_, _) => _statusService.OpenDisplaySettings();
        VolumeStatusButton.Click += (_, _) => ControlCenterRequested?.Invoke(this, EventArgs.Empty);
        ClockButton.Click += (_, _) => ControlCenterRequested?.Invoke(this, EventArgs.Empty);
        ControlCenterButton.Click += (_, _) => ControlCenterRequested?.Invoke(this, EventArgs.Empty);

        _statusService.StatusUpdated += (_, snapshot) => UpdateStatus(snapshot);

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IslandViewModel.TrackTitle) or nameof(IslandViewModel.ArtistName) or nameof(IslandViewModel.IsPlaying))
            {
                UpdateMediaPill();
            }
        };

        PillPlayPauseButton.Click += async (_, _) => await _viewModel.TogglePlayPauseAsync();
    }

    public void UpdateActiveAppTitle(string title)
    {
        ActiveAppTitleTextBlock.Text = string.IsNullOrWhiteSpace(title) ? "Ivory Desktop" : title;
    }

    private void UpdateStatus(SystemStatusSnapshot snapshot)
    {
        TimeTextBlock.Text = snapshot.FormattedTime;
        DateTextBlock.Text = snapshot.FormattedDate;

        WifiGlyph.Text = snapshot.WifiGlyph;
        BatteryGlyph.Text = snapshot.BatteryGlyph;
        BatteryText.Text = $"{snapshot.BatteryPercentage}%";
        BatteryText.Visibility = snapshot.HasBattery ? Visibility.Visible : Visibility.Collapsed;
        VolumeGlyph.Text = snapshot.VolumeGlyph;
    }

    private void UpdateMediaPill()
    {
        if (!_settingsService.Current.ShowMediaInMenuBar ||
            string.IsNullOrWhiteSpace(_viewModel.TrackTitle) ||
            _viewModel.TrackTitle == "No Active Media")
        {
            MediaPillBorder.Visibility = Visibility.Collapsed;
            return;
        }

        MediaPillBorder.Visibility = Visibility.Visible;
        MediaPillText.Text = string.IsNullOrWhiteSpace(_viewModel.ArtistName) || _viewModel.ArtistName.StartsWith("Play music", StringComparison.OrdinalIgnoreCase)
            ? _viewModel.TrackTitle
            : $"{_viewModel.TrackTitle} — {_viewModel.ArtistName}";

        PillPlayPauseGlyph.Text = _viewModel.PlayPauseGlyph;
    }

    public void Reposition()
    {
        if (_isDisposed)
            return;

        var mi = DesktopWindowHelper.GetPrimaryMonitorInfo(_hwnd);
        var work = mi.rcWork;

        // Position across top of primary screen
        int x = work.Left;
        int y = work.Top;
        int width = work.Width;
        int height = (int)Math.Round(32 * DesktopWindowHelper.GetDpiScale(_hwnd));

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            x,
            y,
            width,
            height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    public void SetVisible(bool visible)
    {
        if (_isDisposed)
            return;

        NativeMethods.ShowWindow(_hwnd, visible ? NativeMethods.SW_SHOW : NativeMethods.SW_HIDE);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _glassBackdropService.Dispose();
    }

    private static class NativeMethodsHelper
    {
        public const int SW_SHOWNOACTIVATE = 4;
    }
}
