using System.Diagnostics;
using System.Text;
using IvoryIsland.Interop;
using IvoryIsland.Models;

namespace IvoryIsland.Services;

/// <summary>
/// Enumerates top-level desktop application windows, monitors foreground window changes,
/// and performs safe window switching/restoration via documented Win32 user32 APIs.
/// </summary>
public sealed class WindowManagementService
{
    private readonly HashSet<IntPtr> _ignoredHwnds = new();

    public void RegisterIgnoredWindow(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            _ignoredHwnds.Add(hwnd);
        }
    }

    public bool IsWindowIgnored(IntPtr hwnd) => _ignoredHwnds.Contains(hwnd);

    public List<WindowOverviewItem> EnumerateApplicationWindows()
    {
        var windows = new List<WindowOverviewItem>();

        if (!OperatingSystem.IsWindows())
            return windows;

        try
        {
            IntPtr shellHwnd = NativeMethods.GetShellWindow();
            IntPtr desktopHwnd = NativeMethods.GetDesktopWindow();

            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (hwnd == shellHwnd || hwnd == desktopHwnd || _ignoredHwnds.Contains(hwnd))
                    return true;

                if (!NativeMethods.IsWindowVisible(hwnd))
                    return true;

                nint exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
                if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                    return true;

                int length = NativeMethods.GetWindowTextLength(hwnd);
                if (length <= 0)
                    return true;

                var sb = new StringBuilder(length + 1);
                NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                string title = sb.ToString().Trim();

                if (string.IsNullOrWhiteSpace(title) || title == "Program Manager")
                    return true;

                string procName = string.Empty;
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid != 0)
                {
                    try
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        procName = proc.ProcessName;
                    }
                    catch { }
                }

                windows.Add(new WindowOverviewItem
                {
                    Hwnd = hwnd,
                    Title = title,
                    ProcessName = procName
                });

                return true;
            }, IntPtr.Zero);
        }
        catch (DllNotFoundException) { }

        return windows;
    }

    public static bool SwitchToWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        try
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(hwnd);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string GetActiveWindowTitle()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero)
            return "Ivory Desktop";

        int length = NativeMethods.GetWindowTextLength(fg);
        if (length <= 0)
            return "Ivory Desktop";

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(fg, sb, sb.Capacity);
        string title = sb.ToString().Trim();

        return string.IsNullOrEmpty(title) ? "Ivory Desktop" : title;
    }
}
