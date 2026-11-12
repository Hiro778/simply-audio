using System.Numerics;
using IvoryIsland.Interop;
using IvoryIsland.Models;
using IvoryIsland.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace IvoryIsland.Views;

public sealed partial class VolumeHudWindow : Window, IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly SettingsService _settingsService;
    private readonly GlassBackdropService _glassBackdropService;
    private readonly DispatcherQueueTimer _hideTimer;
    private bool _isDisposed;
    private bool _isVisible;

    public IntPtr WindowHandle => _hwnd;

    public VolumeHudWindow(SettingsService settingsService)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        DesktopWindowHelper.ConfigureOverlayWindow(this, out _hwnd, out _appWindow, roundCorners: true);

        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(_settingsService.Current.GlassOpacity);

        _hideTimer = DispatcherQueue.CreateTimer();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(1400);
        _hideTimer.Tick += (_, _) => HideHud();

        Reposition();
    }

    public void ShowVolume(float volume, bool isMuted)
    {
        if (_isDisposed || !_settingsService.Current.ShowVolumeHud)
            return;

        HudProgressBar.Value = volume;
        HudSpeakerGlyph.Text = isMuted ? "\uE74F" : (volume > 65f ? "\uE995" : (volume > 30f ? "\uE994" : (volume > 0f ? "\uE993" : "\uE74F")));

        _hideTimer.Stop();

        if (!_isVisible)
        {
            _isVisible = true;
            Reposition();
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
            AnimateFade(fadeIn: true);
        }

        _hideTimer.Start();
    }

    private void HideHud()
    {
        if (_isDisposed || !_isVisible)
            return;

        _hideTimer.Stop();
        AnimateFade(fadeIn: false, onComplete: () =>
        {
            _isVisible = false;
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
        });
    }

    private void AnimateFade(bool fadeIn, Action? onComplete = null)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(HudBorder);
        Compositor compositor = visual.Compositor;

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 1.0f), new Vector2(0.4f, 1.0f));
        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(1.0f, fadeIn ? 1.0f : 0.0f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(140); // 120-180ms spec

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        batch.Completed += (_, _) => onComplete?.Invoke();
        visual.StartAnimation(nameof(visual.Opacity), opacityAnim);
        batch.End();
    }

    public void Reposition()
    {
        if (_isDisposed)
            return;

        var mi = DesktopWindowHelper.GetPrimaryMonitorInfo(_hwnd);
        var work = mi.rcWork;
        double scale = DesktopWindowHelper.GetDpiScale(_hwnd);

        int logicalWidth = 210;
        int logicalHeight = 56;

        int physicalW = (int)Math.Round(logicalWidth * scale);
        int physicalH = (int)Math.Round(logicalHeight * scale);

        // Position in lower-middle screen or center
        int x = work.Left + (work.Width - physicalW) / 2;
        int y = work.Bottom - physicalH - (int)Math.Round(110 * scale);

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

        _hideTimer.Stop();
        _glassBackdropService.Dispose();
    }
}
