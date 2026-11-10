using System.Runtime.InteropServices;
using System.Text;
using IvoryIsland.Interop;

namespace IvoryIsland.Services;

/// <summary>
/// Manages the native Win32 Shell_NotifyIcon system tray icon and context menu for Ivory Island.
/// Supports both extracted asset icons and embedded PE icon extraction when running as a standalone
/// Single-File portable executable (IvoryIsland.exe).
/// </summary>
public sealed class SystemTrayService : IDisposable
{
    private const uint TrayIconId = 1001;
    private const nuint CmdToggleVisible = 1;
    private const nuint CmdStartWithWindows = 2;
    private const nuint CmdResetPosition = 3;
    private const nuint CmdOpenSettings = 4;
    private const nuint CmdExit = 5;

    private readonly WindowManager _windowManager;
    private readonly SettingsService _settingsService;
    private NativeMethods.NOTIFYICONDATA _nid;
    private IntPtr _hIcon;
    private bool _isAdded;
    private bool _isDisposed;

    public event EventHandler? ToggleIslandRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ResetPositionRequested;
    public event EventHandler? ExitApplicationRequested;

    public SystemTrayService(WindowManager windowManager, SettingsService settingsService)
    {
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _windowManager.TrayMouseMessageReceived += OnTrayMouseMessageReceived;
        _windowManager.TaskbarRecreated += OnTaskbarRecreated;
    }

    public void Initialize()
    {
        if (_isDisposed)
            return;

        _hIcon = ResolveApplicationIcon();

        _nid = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _windowManager.WindowHandle,
            uID = TrayIconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP | NativeMethods.NIF_SHOWTIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Ivory Island — Floating Media Controller",
            uVersion = NativeMethods.NOTIFYICON_VERSION_4
        };

        AddTrayIcon();
    }

    private static IntPtr ResolveApplicationIcon()
    {
        // 1. Check unpacked Assets\IvoryIsland.ico if present
        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "IvoryIsland.ico");
            if (File.Exists(iconPath))
            {
                IntPtr loaded = NativeMethods.LoadImage(IntPtr.Zero, iconPath, 1, 16, 16, 0x0010);
                if (loaded != IntPtr.Zero)
                    return loaded;
            }
        }
        catch
        {
        }

        // 2. Extract embedded icon directly from the running Single-File EXE (IvoryIsland.exe)
        try
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                var sb = new StringBuilder(exePath, 260);
                ushort iconIndex = 0;
                IntPtr extracted = NativeMethods.ExtractAssociatedIcon(IntPtr.Zero, sb, ref iconIndex);
                if (extracted != IntPtr.Zero)
                    return extracted;
            }
        }
        catch
        {
        }

        // 3. Fallback to standard Windows application icon
        return NativeMethods.LoadIcon(IntPtr.Zero, new IntPtr(32512));
    }

    private void AddTrayIcon()
    {
        if (_isDisposed)
            return;

        _isAdded = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
        if (_isAdded)
        {
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_SETVERSION, ref _nid);
        }
    }

    private void OnTaskbarRecreated(object? sender, EventArgs e)
    {
        AddTrayIcon();
    }

    private void OnTrayMouseMessageReceived(object? sender, uint mouseMessage)
    {
        if (_isDisposed)
            return;

        if (mouseMessage is NativeMethods.WM_LBUTTONUP or NativeMethods.WM_LBUTTONDBLCLK)
        {
            ToggleIslandRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (mouseMessage is NativeMethods.WM_RBUTTONUP or NativeMethods.WM_CONTEXTMENU)
        {
            ShowNativeContextMenu();
        }
    }

    private void ShowNativeContextMenu()
    {
        IntPtr hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == IntPtr.Zero)
            return;

        try
        {
            bool islandEnabled = _settingsService.Current.IsIslandEnabled;
            bool startWithWin = _settingsService.Current.StartWithWindows;

            NativeMethods.AppendMenu(
                hMenu,
                NativeMethods.MF_STRING,
                CmdToggleVisible,
                islandEnabled ? "Hide Ivory Island" : "Show Ivory Island");

            NativeMethods.AppendMenu(
                hMenu,
                NativeMethods.MF_STRING | (startWithWin ? NativeMethods.MF_CHECKED : 0),
                CmdStartWithWindows,
                "Start with Windows");

            NativeMethods.AppendMenu(
                hMenu,
                NativeMethods.MF_STRING,
                CmdResetPosition,
                "Reset Position to Top Center");

            NativeMethods.AppendMenu(
                hMenu,
                NativeMethods.MF_STRING,
                CmdOpenSettings,
                "Settings & Data Storage...");

            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, 0, null);

            NativeMethods.AppendMenu(
                hMenu,
                NativeMethods.MF_STRING,
                CmdExit,
                "Exit Ivory Island");

            NativeMethods.GetCursorPos(out var pt);
            NativeMethods.SetForegroundWindow(_windowManager.WindowHandle);

            int selectedCmd = NativeMethods.TrackPopupMenuEx(
                hMenu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_BOTTOMALIGN,
                pt.X,
                pt.Y,
                _windowManager.WindowHandle,
                IntPtr.Zero);

            NativeMethods.PostMessage(_windowManager.WindowHandle, 0, IntPtr.Zero, IntPtr.Zero);

            switch ((nuint)selectedCmd)
            {
                case CmdToggleVisible:
                    ToggleIslandRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CmdStartWithWindows:
                    _settingsService.SetStartWithWindows(!startWithWin);
                    break;
                case CmdResetPosition:
                    ResetPositionRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CmdOpenSettings:
                    OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CmdExit:
                    ExitApplicationRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _windowManager.TrayMouseMessageReceived -= OnTrayMouseMessageReceived;
        _windowManager.TaskbarRecreated -= OnTaskbarRecreated;

        if (_isAdded)
        {
            try
            {
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);
            }
            catch
            {
            }
            _isAdded = false;
        }
    }
}
