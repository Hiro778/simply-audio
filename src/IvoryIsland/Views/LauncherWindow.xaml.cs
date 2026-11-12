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

public sealed partial class LauncherWindow : Window, IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly SettingsService _settingsService;
    private readonly GlassBackdropService _glassBackdropService;
    private readonly List<InstalledAppItem> _allApps = new();
    private bool _isDisposed;
    private bool _isVisible;

    public IntPtr WindowHandle => _hwnd;
    public bool IsLauncherOpen => _isVisible;

    public event EventHandler? LauncherClosed;

    public LauncherWindow(SettingsService settingsService)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        DesktopWindowHelper.ConfigureOverlayWindow(this, out _hwnd, out _appWindow, roundCorners: true);

        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(_settingsService.Current.GlassOpacity);

        RootLauncherGrid.KeyDown += OnKeyDown;
        Reposition();
    }

    public void SetApplications(IEnumerable<InstalledAppItem> apps)
    {
        _allApps.Clear();
        _allApps.AddRange(apps);
        ApplyFilter(string.Empty);
    }

    private void ApplyFilter(string query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allApps
            : _allApps.Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        AppsGridView.ItemsSource = filtered;
        AppCountTextBlock.Text = $"{filtered.Count} applications discovered";
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void OnAppItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is InstalledAppItem app)
        {
            AppDiscoveryService.LaunchApp(app);
            HideLauncher();
        }
    }

    private void OnBackgroundPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // If user clicks the transparent background outside the container border, close launcher
        var point = e.GetCurrentPoint(LauncherContainerBorder);
        if (point.Position.X < 0 || point.Position.X > LauncherContainerBorder.ActualWidth ||
            point.Position.Y < 0 || point.Position.Y > LauncherContainerBorder.ActualHeight)
        {
            HideLauncher();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        HideLauncher();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            HideLauncher();
            e.Handled = true;
        }
    }

    public void ShowLauncher()
    {
        if (_isDisposed)
            return;

        _isVisible = true;
        Reposition();
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
        NativeMethods.SetForegroundWindow(_hwnd);

        SearchBox.Text = string.Empty;
        SearchBox.Focus(FocusState.Programmatic);

        AnimateEntrance();
    }

    public void HideLauncher()
    {
        if (_isDisposed || !_isVisible)
            return;

        _isVisible = false;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
        LauncherClosed?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleLauncher()
    {
        if (_isVisible)
        {
            HideLauncher();
        }
        else
        {
            ShowLauncher();
        }
    }

    private void AnimateEntrance()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(LauncherContainerBorder);
        Compositor compositor = visual.Compositor;

        Vector2 size = LauncherContainerBorder.ActualSize;
        if (size.X > 0 && size.Y > 0)
        {
            visual.CenterPoint = new Vector3(size.X * 0.5f, size.Y * 0.5f, 0.0f);
        }

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1.0f), new Vector2(0.3f, 1.0f));

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0.0f, 0.2f);
        opacityAnim.InsertKeyFrame(1.0f, 1.0f, easing);
        opacityAnim.Duration = TimeSpan.FromMilliseconds(220); // 180-260ms spec

        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0.0f, new Vector3(0.92f, 0.92f, 1.0f));
        scaleAnim.InsertKeyFrame(1.0f, Vector3.One, easing);
        scaleAnim.Duration = TimeSpan.FromMilliseconds(220);

        visual.StartAnimation(nameof(visual.Opacity), opacityAnim);
        visual.StartAnimation(nameof(visual.Scale), scaleAnim);
    }

    public void Reposition()
    {
        if (_isDisposed)
            return;

        var mi = DesktopWindowHelper.GetPrimaryMonitorInfo(_hwnd);
        var work = mi.rcWork;

        // Position full screen so clicking background works
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
