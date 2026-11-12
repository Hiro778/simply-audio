using System.Runtime.InteropServices;
using IvoryIsland.Interop;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace IvoryIsland.Services;

/// <summary>
/// Configures native WinUI 3 Window instances as non-activating, topmost, borderless
/// desktop overlays with per-monitor DPI positioning and modern DWM rounded corners.
/// </summary>
public static class DesktopWindowHelper
{
    public static void ConfigureOverlayWindow(
        Window window,
        out IntPtr hwnd,
        out AppWindow appWindow,
        bool roundCorners = true)
    {
        hwnd = WindowNative.GetWindowHandle(window);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        appWindow.IsShownInSwitchers = false;

        // Apply extended styles: WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST
        nint exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST;
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        if (roundCorners)
        {
            int cornerPref = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref cornerPref,
                sizeof(int));
        }
    }

    public static double GetDpiScale(IntPtr hwnd)
    {
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi > 0 ? dpi / 96.0 : 1.0;
    }

    public static NativeMethods.MONITORINFOEX GetPrimaryMonitorInfo(IntPtr hwnd)
    {
        IntPtr hMon = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(hMon, ref mi))
        {
            mi.rcMonitor = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            mi.rcWork = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        }
        return mi;
    }
}
