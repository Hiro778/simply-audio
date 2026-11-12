using System.Numerics;
using IvoryIsland.Interop;
using IvoryIsland.Models;
using IvoryIsland.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace IvoryIsland.Views;

public sealed partial class WindowOverviewWindow : Window, IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly SettingsService _settingsService;
    private readonly GlassBackdropService _glassBackdropService;
    private bool _isDisposed;
    private bool _isVisible;

    public IntPtr WindowHandle => _hwnd;
    public bool IsOpen => _isVisible;

    public event EventHandler? OverviewClosed;

    public WindowOverviewWindow(SettingsService settingsService)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        DesktopWindowHelper.ConfigureOverlayWindow(this, out _hwnd, out _appWindow, roundCorners: true);

        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(_settingsService.Current.GlassOpacity);

        RootOverviewGrid.KeyDown += OnKeyDown;
        Reposition();
    }

    public void SetWindows(IEnumerable<WindowOverviewItem> windows)
    {
        var list = windows.ToList();
        WindowsGridView.ItemsSource = list;
        WindowsCountTextBlock.Text = $"{list.Count} active windows";
    }

    private void OnWindowItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WindowOverviewItem item)
        {
            WindowManagementService.SwitchToWindow(item.Hwnd);
            HideOverview();
        }
    }

    private void OnBackgroundPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(OverviewContainerBorder);
        if (point.Position.X < 0 || point.Position.X > OverviewContainerBorder.ActualWidth ||
            point.Position.Y < 0 || point.Position.Y > OverviewContainerBorder.ActualHeight)
        {
            HideOverview();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        HideOverview();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            HideOverview();
            e.Handled = true;
        }
    }

    public void ShowOverview(IEnumerable<WindowOverviewItem> windows)
    {
        if (_isDisposed)
            return;

        _isVisible = true;
        SetWindows(windows);
        Reposition();
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
        NativeMethods.SetForegroundWindow(_hwnd);
        AnimateEntrance();
    }

    public void HideOverview()
    {
        if (_isDisposed || !_isVisible)
            return;

        _isVisible = false;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
        OverviewClosed?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleOverview(IEnumerable<WindowOverviewItem> windows)
    {
        if (_isVisible)
        {
            HideOverview();
        }
        else
        {
            ShowOverview(windows);
        }
    }

    private void AnimateEntrance()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(OverviewContainerBorder);
        Compositor compositor = visual.Compositor;

        Vector2 size = OverviewContainerBorder.ActualSize;
        if (size.X > 0 && size.Y > 0)
        {
            visual.CenterPoint = new Vector3(size.X * 0.5f, size.Y * 0.5f, 0.0f);
        }

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1.0f), new Vector2(0.3f, 1.0f));

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0.0f, 0.2f);
        opacityAnim.InsertKeyFrame(1.0f, 1.0f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(240); // 200-300ms spec

        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0.0f, new Vector3(0.93f, 0.93f, 1.0f));
        scaleAnim.InsertKeyFrame(1.0f, Vector3.One, easing);
        scaleAnim.Duration = TimeSpan.FromMilliseconds(240);

        visual.StartAnimation(nameof(visual.Opacity), opacityAnim);
        visual.StartAnimation(nameof(visual.Scale), scaleAnim);
    }

    public void Reposition()
    {
        if (_isDisposed)
            return;

        var mi = DesktopWindowHelper.GetPrimaryMonitorInfo(_hwnd);
        var work = mi.rcWork;

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            work.Left,
            work.Top,
            work.Width,
            work.Height,
            NativeMethods.SWP_NOACTIVATE);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _glassBackdropService.Dispose();
    }
}
