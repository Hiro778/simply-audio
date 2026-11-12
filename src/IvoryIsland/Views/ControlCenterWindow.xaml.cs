using System.Numerics;
using IvoryIsland.Interop;
using IvoryIsland.Models;
using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;

namespace IvoryIsland.Views;

public sealed partial class ControlCenterWindow : Window, IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly SettingsService _settingsService;
    private readonly SystemStatusService _statusService;
    private readonly IslandViewModel _viewModel;
    private readonly GlassBackdropService _glassBackdropService;
    private bool _isDisposed;
    private bool _isVisible;
    private bool _isInternalVolumeUpdate;

    public IntPtr WindowHandle => _hwnd;
    public bool IsOpen => _isVisible;

    public event EventHandler? ToggleModeRequested;
    public event EventHandler? ExitRequested;

    public ControlCenterWindow(
        SettingsService settingsService,
        SystemStatusService statusService,
        IslandViewModel viewModel)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        DesktopWindowHelper.ConfigureOverlayWindow(this, out _hwnd, out _appWindow, roundCorners: true);

        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(_settingsService.Current.GlassOpacity);

        WireEvents();
        UpdateStatus(_statusService.Current);
        Reposition();
    }

    private void WireEvents()
    {
        WifiRowButton.Click += (_, _) => _statusService.OpenWifiSettings();
        BluetoothRowButton.Click += (_, _) => _statusService.OpenBluetoothSettings();
        ModeToggleButton.Click += (_, _) => ToggleModeRequested?.Invoke(this, EventArgs.Empty);

        MuteButton.Click += (_, _) => _statusService.ToggleMute();
        VolumeSlider.ValueChanged += OnVolumeSliderValueChanged;
        BrightnessSlider.ValueChanged += (_, _) => _statusService.OpenDisplaySettings();

        PlayPauseButton.Click += async (_, _) => await _viewModel.TogglePlayPauseAsync();
        PrevTrackButton.Click += async (_, _) => await _viewModel.SkipPreviousAsync();
        NextTrackButton.Click += async (_, _) => await _viewModel.SkipNextAsync();

        TaskMgrButton.Click += (_, _) => EmergencyRecoveryService.LaunchTaskManager();
        TaskbarSettingsButton.Click += (_, _) => _statusService.OpenTaskbarSettings();
        RecoveryExitButton.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _statusService.StatusUpdated += (_, snapshot) => UpdateStatus(snapshot);

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IslandViewModel.TrackTitle) or nameof(IslandViewModel.ArtistName) or nameof(IslandViewModel.IsPlaying) or nameof(IslandViewModel.AlbumArtwork))
            {
                UpdateMediaInfo();
            }
        };

        UpdateMediaInfo();
    }

    private void OnVolumeSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInternalVolumeUpdate)
            return;

        _statusService.SetVolume((float)e.NewValue);
    }

    private void UpdateStatus(SystemStatusSnapshot snapshot)
    {
        WifiGlyph.Text = snapshot.WifiGlyph;
        WifiSsidText.Text = snapshot.WifiSsid;

        BatteryGlyph.Text = snapshot.BatteryGlyph;
        BatteryPercentText.Text = $"{snapshot.BatteryPercentage}%";

        _isInternalVolumeUpdate = true;
        VolumeSlider.Value = snapshot.Volume;
        _isInternalVolumeUpdate = false;

        VolumePercentText.Text = $"{(int)Math.Round(snapshot.Volume)}%";
        VolumeGlyph.Text = snapshot.VolumeGlyph;

        ModeText.Text = _settingsService.Current.OperatingMode == DesktopOperatingMode.Immersive ? "Immersive" : "Standard";
    }

    private void UpdateMediaInfo()
    {
        NowPlayingTitle.Text = string.IsNullOrWhiteSpace(_viewModel.TrackTitle) ? "Not Playing" : _viewModel.TrackTitle;
        NowPlayingArtist.Text = string.IsNullOrWhiteSpace(_viewModel.ArtistName) ? "Ivory Island" : _viewModel.ArtistName;
        PlayPauseGlyph.Text = _viewModel.PlayPauseGlyph;

        if (_viewModel.AlbumArtwork is not null)
        {
            AlbumArtImage.Source = _viewModel.AlbumArtwork;
        }
    }

    public void ShowControlCenter()
    {
        if (_isDisposed)
            return;

        _isVisible = true;
        Reposition();
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
        AnimateEntrance();
    }

    public void HideControlCenter()
    {
        if (_isDisposed || !_isVisible)
            return;

        _isVisible = false;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
    }

    public void ToggleControlCenter()
    {
        if (_isVisible)
        {
            HideControlCenter();
        }
        else
        {
            ShowControlCenter();
        }
    }

    private void AnimateEntrance()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(ControlCenterBorder);
        Compositor compositor = visual.Compositor;

        Vector2 size = ControlCenterBorder.ActualSize;
        if (size.X > 0 && size.Y > 0)
        {
            visual.CenterPoint = new Vector3(size.X * 0.85f, 0.0f, 0.0f);
        }

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1.0f), new Vector2(0.3f, 1.0f));

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0.0f, 0.3f);
        opacityAnim.InsertKeyFrame(1.0f, 1.0f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(200); // 180-240ms spec

        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0.0f, new Vector3(0.94f, 0.94f, 1.0f));
        scaleAnim.InsertKeyFrame(1.0f, Vector3.One, easing);
        scaleAnim.Duration = TimeSpan.FromMilliseconds(200);

        visual.StartAnimation(nameof(visual.Opacity), opacityAnim);
        visual.StartAnimation(nameof(visual.Scale), scaleAnim);
    }

    public void Reposition()
    {
        if (_isDisposed)
            return;

        var mi = DesktopWindowHelper.GetPrimaryMonitorInfo(_hwnd);
        var work = mi.rcWork;
        double scale = DesktopWindowHelper.GetDpiScale(_hwnd);

        int logicalWidth = 340;
        int logicalHeight = 540;

        int physicalW = (int)Math.Round(logicalWidth * scale);
        int physicalH = (int)Math.Round(logicalHeight * scale);

        int x = work.Right - physicalW - (int)Math.Round(16 * scale);
        int y = work.Top + (int)Math.Round(36 * scale); // Anchored right below the top menu bar

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            x,
            y,
            physicalW,
            physicalH,
            NativeMethods.SWP_NOACTIVATE | (_isVisible ? NativeMethods.SWP_SHOWWINDOW : NativeMethods.SWP_HIDEWINDOW));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _glassBackdropService.Dispose();
    }
}
