using System.Runtime.InteropServices;
using IvoryIsland.Interop;
using IvoryIsland.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace IvoryIsland.Services;

/// <summary>
/// Manages the native Win32 floating overlay lifecycle for Ivory Island:
/// - Applies WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE so the island never appears in Alt-Tab or steals keyboard focus.
/// - Intercepts WM_MOUSEACTIVATE (returning MA_NOACTIVATE) while preserving full click/slider interactivity.
/// - Positions the capsule at top-center (or custom dragged position) within the active display's work area.
/// - Handles per-monitor DPI scaling, monitor changes, session lock/unlock, sleep/resume, and fullscreen auto-hide.
/// </summary>
public sealed class WindowManager : IDisposable
{
    private readonly Window _window;
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly SettingsService _settingsService;

    private NativeMethods.WndProcDelegate? _wndProcDelegate;
    private IntPtr _prevWndProc;
    private NativeMethods.WinEventDelegate? _winEventDelegate;
    private IntPtr _foregroundHook;
    private IntPtr _locationHook;
    private uint _taskbarCreatedMsg;

    private bool _isDragging;
    private NativeMethods.POINT _dragStartCursor;
    private int _dragStartWindowX;
    private int _dragStartWindowY;

    private int _logicalWidth = 780;
    private int _logicalHeight = 88;
    private bool _isFullscreenObstructed;
    private bool _isUserHidden;
    private bool _isSessionLocked;
    private bool _isDisposed;

    public IntPtr WindowHandle => _hwnd;
    public AppWindow AppWindow => _appWindow;
    public bool IsFullscreenObstructed => _isFullscreenObstructed;

    public event EventHandler? TaskbarRecreated;
    public event EventHandler<uint>? TrayMouseMessageReceived;
    public event EventHandler? SystemResumeOrUnlockDetected;
    public event EventHandler<bool>? FullscreenObstructionChanged;

    public WindowManager(Window window, SettingsService settingsService)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _hwnd = WindowNative.GetWindowHandle(_window);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
    }

    public void InitializeOverlayWindow()
    {
        if (_isDisposed)
            return;

        // 1. Configure borderless topmost OverlappedPresenter
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _appWindow.IsShownInSwitchers = false;

        // 2. Apply Win32 extended styles: WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST
        nint exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST;
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        // 3. Request rounded corners on Windows 11 without failing on Windows 10
        int cornerPref = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(
            _hwnd,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref cornerPref,
            sizeof(int));

        // 4. Register TaskbarCreated message for Explorer restart recovery and WTS session lock notifications
        _taskbarCreatedMsg = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        NativeMethods.WTSRegisterSessionNotification(_hwnd, NativeMethods.NOTIFY_FOR_THIS_SESSION);

        // 5. Subclass WndProc for WM_MOUSEACTIVATE (MA_NOACTIVATE), DPI changes, and Tray notifications
        _wndProcDelegate = CustomWndProc;
        _prevWndProc = NativeMethods.SetWindowLongPtr(
            _hwnd,
            NativeMethods.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        // 6. Install lightweight out-of-context WinEventHook for event-driven fullscreen detection
        _winEventDelegate = OnWinEventCallback;
        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _locationHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        // 7. Apply initial size and position at Top-Center of active display
        UpdateSizeAndPosition(_logicalWidth, _logicalHeight, preserveCustomPosition: true);
    }

    public double GetDpiScale()
    {
        uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
        return dpi > 0 ? dpi / 96.0 : 1.0;
    }

    public void UpdateSizeAndPosition(int logicalWidth, int logicalHeight, bool preserveCustomPosition = true)
    {
        if (_isDisposed)
            return;

        _logicalWidth = Math.Clamp(logicalWidth, 180, 860);
        _logicalHeight = Math.Clamp(logicalHeight, 48, 320);

        double scale = GetDpiScale();
        int physicalW = (int)Math.Round(_logicalWidth * scale);
        int physicalH = (int)Math.Round(_logicalHeight * scale);

        var monitorInfo = GetTargetMonitorInfo();
        var workArea = monitorInfo.rcWork;
        int topMarginPhysical = (int)Math.Round(Math.Clamp(_settingsService.Current.TopMarginPx, 10, 48) * scale);

        int targetX;
        int targetY;

        var settings = _settingsService.Current;
        if (preserveCustomPosition &&
            settings.PositionAlignment == TopPositionAlignment.CustomDragged &&
            settings.CustomX.HasValue &&
            settings.CustomY.HasValue)
        {
            targetX = settings.CustomX.Value;
            targetY = settings.CustomY.Value;
        }
        else
        {
            targetY = workArea.Top + topMarginPhysical;
            targetX = settings.PositionAlignment switch
            {
                TopPositionAlignment.TopLeft => workArea.Left + (int)Math.Round(24 * scale),
                TopPositionAlignment.TopRight => workArea.Right - physicalW - (int)Math.Round(24 * scale),
                _ => workArea.Left + (workArea.Width - physicalW) / 2
            };
        }

        // Clamp strictly inside the monitor's usable work area so the capsule never goes off-screen
        int minX = workArea.Left + 8;
        int maxX = Math.Max(minX, workArea.Right - physicalW - 8);
        int minY = workArea.Top + 6;
        int maxY = Math.Max(minY, workArea.Bottom - physicalH - 8);

        targetX = Math.Clamp(targetX, minX, maxX);
        targetY = Math.Clamp(targetY, minY, maxY);

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            targetX,
            targetY,
            physicalW,
            physicalH,
            NativeMethods.SWP_NOACTIVATE);
    }

    public void BeginDrag()
    {
        if (_isDisposed)
            return;

        if (NativeMethods.GetCursorPos(out var cursor) && NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            _isDragging = true;
            _dragStartCursor = cursor;
            _dragStartWindowX = rect.Left;
            _dragStartWindowY = rect.Top;
        }
    }

    public void UpdateDrag()
    {
        if (!_isDragging || _isDisposed)
            return;

        if (!NativeMethods.GetCursorPos(out var cursor))
            return;

        int dx = cursor.X - _dragStartCursor.X;
        int dy = cursor.Y - _dragStartCursor.Y;

        int newX = _dragStartWindowX + dx;
        int newY = _dragStartWindowY + dy;

        double scale = GetDpiScale();
        int physicalW = (int)Math.Round(_logicalWidth * scale);
        int physicalH = (int)Math.Round(_logicalHeight * scale);

        IntPtr hMon = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (NativeMethods.GetMonitorInfo(hMon, ref mi))
        {
            var work = mi.rcWork;
            newX = Math.Clamp(newX, work.Left + 8, Math.Max(work.Left + 8, work.Right - physicalW - 8));
            newY = Math.Clamp(newY, work.Top + 6, Math.Max(work.Top + 6, work.Bottom - physicalH - 8));
        }

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            newX,
            newY,
            physicalW,
            physicalH,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOSIZE);
    }

    public void EndDrag()
    {
        if (!_isDragging || _isDisposed)
            return;

        _isDragging = false;

        if (NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            var settings = _settingsService.Current;
            settings.PositionAlignment = TopPositionAlignment.CustomDragged;
            settings.CustomX = rect.Left;
            settings.CustomY = rect.Top;
            _settingsService.Save();
        }
    }

    public void ResetToTopCenter()
    {
        var settings = _settingsService.Current;
        settings.PositionAlignment = TopPositionAlignment.TopCenter;
        settings.CustomX = null;
        settings.CustomY = null;
        _settingsService.Save();

        UpdateSizeAndPosition(_logicalWidth, _logicalHeight, preserveCustomPosition: false);
    }

    public void SetOverlayVisible(bool visible)
    {
        if (_isDisposed)
            return;

        _isUserHidden = !visible;
        ApplyEffectiveVisibility();
    }

    private void ApplyEffectiveVisibility()
    {
        if (_isDisposed)
            return;

        bool shouldShow = !_isUserHidden &&
                          !_isSessionLocked &&
                          _settingsService.Current.IsIslandEnabled &&
                          !(_settingsService.Current.AutoHideInFullscreen && _isFullscreenObstructed);

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE |
            (shouldShow ? NativeMethods.SWP_SHOWWINDOW : NativeMethods.SWP_HIDEWINDOW));
    }

    public void EvaluateFullscreenState()
    {
        if (_isDisposed)
            return;

        bool obstructed = DetectFullscreenApplicationOnCurrentMonitor();
        if (obstructed != _isFullscreenObstructed)
        {
            _isFullscreenObstructed = obstructed;
            ApplyEffectiveVisibility();
            FullscreenObstructionChanged?.Invoke(this, obstructed);
        }
    }

    private bool DetectFullscreenApplicationOnCurrentMonitor()
    {
        if (!_settingsService.Current.AutoHideInFullscreen)
            return false;

        try
        {
            // 1. Check SHQueryUserNotificationState for exclusive D3D fullscreen or presentation mode
            if (NativeMethods.SHQueryUserNotificationState(out var quns) == 0)
            {
                if (quns is NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN
                    or NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE)
                {
                    return true;
                }
            }

            // 2. Check if the foreground window covers the entire monitor bounds where Ivory Island resides
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero ||
                fgHwnd == _hwnd ||
                fgHwnd == NativeMethods.GetDesktopWindow() ||
                fgHwnd == NativeMethods.GetShellWindow())
            {
                return false;
            }

            if (!NativeMethods.GetWindowRect(fgHwnd, out var fgRect))
                return false;

            IntPtr islandMon = NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            IntPtr fgMon = NativeMethods.MonitorFromWindow(fgHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (islandMon != fgMon)
                return false;

            var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
            if (!NativeMethods.GetMonitorInfo(islandMon, ref mi))
                return false;

            var monRect = mi.rcMonitor;
            bool coversFullMonitor =
                fgRect.Left <= monRect.Left &&
                fgRect.Top <= monRect.Top &&
                fgRect.Right >= monRect.Right &&
                fgRect.Bottom >= monRect.Bottom;

            return coversFullMonitor;
        }
        catch
        {
            return false;
        }
    }

    private NativeMethods.MONITORINFOEX GetTargetMonitorInfo()
    {
        IntPtr hMon = NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMon == IntPtr.Zero)
        {
            NativeMethods.GetCursorPos(out var pt);
            hMon = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        }

        var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(hMon, ref mi))
        {
            mi.rcMonitor = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            mi.rcWork = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1032 };
        }
        return mi;
    }

    private void OnWinEventCallback(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (_isDisposed || idObject != 0 || hwnd == IntPtr.Zero || hwnd == _hwnd)
            return;

        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND || hwnd == NativeMethods.GetForegroundWindow())
        {
            EvaluateFullscreenState();
        }
    }

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_MOUSEACTIVATE:
                // Prevent Ivory Island from stealing keyboard focus from the user's active application
                // while allowing buttons and sliders inside the capsule to receive pointer events normally.
                return new IntPtr(NativeMethods.MA_NOACTIVATE);

            case NativeMethods.WM_DPICHANGED:
            case NativeMethods.WM_DISPLAYCHANGE:
                UpdateSizeAndPosition(_logicalWidth, _logicalHeight, preserveCustomPosition: true);
                break;

            case NativeMethods.WM_WTSSESSION_CHANGE:
                int sessionCode = wParam.ToInt32();
                if (sessionCode == NativeMethods.WTS_SESSION_LOCK)
                {
                    _isSessionLocked = true;
                    ApplyEffectiveVisibility();
                }
                else if (sessionCode == NativeMethods.WTS_SESSION_UNLOCK)
                {
                    _isSessionLocked = false;
                    ApplyEffectiveVisibility();
                    SystemResumeOrUnlockDetected?.Invoke(this, EventArgs.Empty);
                }
                break;

            case NativeMethods.WM_POWERBROADCAST:
                if (wParam.ToInt32() == NativeMethods.PBT_APMRESUMEAUTOMATIC)
                {
                    SystemResumeOrUnlockDetected?.Invoke(this, EventArgs.Empty);
                }
                break;

            case NativeMethods.WM_TRAYICON:
                uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
                TrayMouseMessageReceived?.Invoke(this, mouseMsg);
                return IntPtr.Zero;
        }

        if (_taskbarCreatedMsg != 0 && msg == _taskbarCreatedMsg)
        {
            TaskbarRecreated?.Invoke(this, EventArgs.Empty);
        }

        return NativeMethods.CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        try
        {
            if (_foregroundHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_foregroundHook);
                _foregroundHook = IntPtr.Zero;
            }
            if (_locationHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_locationHook);
                _locationHook = IntPtr.Zero;
            }
        }
        catch { }

        try
        {
            NativeMethods.WTSUnRegisterSessionNotification(_hwnd);
        }
        catch { }

        try
        {
            if (_prevWndProc != IntPtr.Zero)
            {
                NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWLP_WNDPROC, _prevWndProc);
                _prevWndProc = IntPtr.Zero;
            }
        }
        catch { }
    }
}
