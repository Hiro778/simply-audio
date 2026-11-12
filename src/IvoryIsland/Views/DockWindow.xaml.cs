using System.Diagnostics;
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
using Microsoft.UI.Xaml.Media;

namespace IvoryIsland.Views;

public sealed partial class DockWindow : Window, IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly SettingsService _settingsService;
    private readonly GlassBackdropService _glassBackdropService;
    private readonly List<FrameworkElement> _dockItemElements = new();
    private bool _isDisposed;

    public IntPtr WindowHandle => _hwnd;

    public event EventHandler? LaunchpadRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ToggleMediaRequested;
    public event EventHandler<WindowOverviewItem>? SwitchToWindowRequested;

    public DockWindow(SettingsService settingsService)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        DesktopWindowHelper.ConfigureOverlayWindow(this, out _hwnd, out _appWindow, roundCorners: true);

        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(_settingsService.Current.GlassOpacity);

        CollectDockItems();
        WireEvents();
        Reposition();
    }

    private void CollectDockItems()
    {
        _dockItemElements.Clear();
        _dockItemElements.Add(DockItemFinder);
        _dockItemElements.Add(DockItemLaunchpad);
        _dockItemElements.Add(DockItemBrowser);
        _dockItemElements.Add(DockItemTerminal);
        _dockItemElements.Add(DockItemMedia);
        _dockItemElements.Add(DockItemSettings);
        _dockItemElements.Add(DockItemTrash);
    }

    private void WireEvents()
    {
        BtnFinder.Click += (_, _) => LaunchProcess("explorer.exe");
        BtnLaunchpad.Click += (_, _) => LaunchpadRequested?.Invoke(this, EventArgs.Empty);
        BtnBrowser.Click += (_, _) => LaunchUrl("https://www.google.com");
        BtnTerminal.Click += (_, _) => LaunchTerminal();
        BtnMedia.Click += (_, _) => ToggleMediaRequested?.Invoke(this, EventArgs.Empty);
        BtnSettings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        BtnTrash.Click += (_, _) => LaunchProcess("explorer.exe", "shell:RecycleBinFolder");
    }

    public void PopulateRunningApps(IEnumerable<WindowOverviewItem> windows)
    {
        RunningAppsStackPanel.Children.Clear();

        var distinctApps = windows
            .GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Take(8);

        foreach (var group in distinctApps)
        {
            var firstWin = group.First();
            var itemGrid = new Grid
            {
                Width = 48,
                Height = 58,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = firstWin.Title
            };

            var btn = new Button
            {
                Style = Application.Current.Resources.TryGetValue("DockItemButtonStyle", out var styleObj) ? (Style)styleObj : null,
                Width = 48,
                Height = 48
            };
            ToolTipService.SetToolTip(btn, firstWin.Title);

            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Application.Current.Resources.TryGetValue("IvoryGlassSecondaryBrush", out var bgObj) ? (Brush)bgObj : new SolidColorBrush(Microsoft.UI.Colors.White),
                BorderBrush = Application.Current.Resources.TryGetValue("IvoryBorderSubtleBrush", out var borderObj) ? (Brush)borderObj : null,
                BorderThickness = new Thickness(1),
                Width = 44,
                Height = 44
            };

            var iconText = new TextBlock
            {
                Text = "\uE71D",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 20,
                Foreground = Application.Current.Resources.TryGetValue("IvoryPrimaryTextBrush", out var fgObj) ? (Brush)fgObj : new SolidColorBrush(Microsoft.UI.Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = iconText;
            btn.Content = border;

            var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = Application.Current.Resources.TryGetValue("RunningAppDotBrush", out var dotObj) ? (Brush)dotObj : new SolidColorBrush(Microsoft.UI.Colors.Purple),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2)
            };

            itemGrid.Children.Add(btn);
            itemGrid.Children.Add(dot);

            btn.Click += (_, _) =>
            {
                SwitchToWindowRequested?.Invoke(this, firstWin);
                WindowManagementService.SwitchToWindow(firstWin.Hwnd);
            };

            RunningAppsStackPanel.Children.Add(itemGrid);
            _dockItemElements.Add(itemGrid);
        }

        Reposition();
    }

    private void OnDockPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_settingsService.Current.DockMagnification)
            return;

        var pointerPos = e.GetCurrentPoint(DockItemsHost).Position;

        foreach (var item in _dockItemElements)
        {
            try
            {
                var transform = item.TransformToVisual(DockItemsHost);
                var itemPos = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                double centerX = itemPos.X + item.ActualWidth * 0.5;

                double distance = Math.Abs(pointerPos.X - centerX);
                // Gaussian magnification curve: peak 1.32x at cursor, spreading smoothly ~55px
                double factor = Math.Exp(-Math.Pow(distance / 55.0, 2.0));
                float targetScale = (float)(1.0 + 0.32 * factor);

                AnimateItemScale(item, targetScale);
            }
            catch { }
        }
    }

    private void OnDockPointerExited(object sender, PointerRoutedEventArgs e)
    {
        foreach (var item in _dockItemElements)
        {
            AnimateItemScale(item, 1.0f);
        }
    }

    private static void AnimateItemScale(FrameworkElement element, float targetScale)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;

        Vector2 size = element.ActualSize;
        if (size.X > 0 && size.Y > 0)
        {
            visual.CenterPoint = new Vector3(size.X * 0.5f, size.Y * 0.85f, 0.0f);
        }

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 1.0f), new Vector2(0.4f, 1.0f));
        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(1.0f, new Vector3(targetScale, targetScale, 1.0f), easing);
        scaleAnim.Duration = TimeSpan.FromMilliseconds(140);

        visual.StartAnimation(nameof(visual.Scale), scaleAnim);
    }

    public void Reposition()
    {
        if (_isDisposed)
            return;

        var mi = DesktopWindowHelper.GetPrimaryMonitorInfo(_hwnd);
        var work = mi.rcWork;
        double scale = DesktopWindowHelper.GetDpiScale(_hwnd);

        // Calculate dynamic width based on dock items count
        int itemCount = 7 + RunningAppsStackPanel.Children.Count;
        int logicalWidth = Math.Clamp(itemCount * 56 + 50, 460, 960);
        int logicalHeight = 84;

        int physicalW = (int)Math.Round(logicalWidth * scale);
        int physicalH = (int)Math.Round(logicalHeight * scale);

        int x = work.Left + (work.Width - physicalW) / 2;
        int y = work.Bottom - physicalH - (int)Math.Round(6 * scale);

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            x,
            y,
            physicalW,
            physicalH,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    public void SetVisible(bool visible)
    {
        if (_isDisposed)
            return;

        NativeMethods.ShowWindow(_hwnd, visible ? NativeMethods.SW_SHOW : NativeMethods.SW_HIDE);
    }

    private static void LaunchProcess(string fileName, string? args = null)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, args ?? string.Empty) { UseShellExecute = true };
            Process.Start(psi);
        }
        catch { }
    }

    private static void LaunchUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private static void LaunchTerminal()
    {
        try
        {
            Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe") { UseShellExecute = true });
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _glassBackdropService.Dispose();
    }
}
