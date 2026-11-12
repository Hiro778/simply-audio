using System.Diagnostics;
using IvoryIsland.Interop;
using IvoryIsland.Models;

namespace IvoryIsland.Services;

/// <summary>
/// Provides guaranteed safe recovery mechanisms and the global emergency exit hotkey
/// (Ctrl+Alt+Escape) so the user's desktop is never left stranded or inaccessible.
/// </summary>
public sealed class EmergencyRecoveryService : IDisposable
{
    private const int HotkeyId = 0x9527;
    private IntPtr _registeredHwnd = IntPtr.Zero;
    private bool _isDisposed;

    public event EventHandler? EmergencyTriggered;

    public bool RegisterEmergencyHotkey(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _isDisposed)
            return false;

        _registeredHwnd = hwnd;
        return NativeMethods.RegisterHotKey(
            hwnd,
            HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_ESCAPE);
    }

    public void UnregisterEmergencyHotkey()
    {
        if (_registeredHwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_registeredHwnd, HotkeyId);
            _registeredHwnd = IntPtr.Zero;
        }
    }

    public void HandleHotkeyMessage(int id)
    {
        if (id == HotkeyId)
        {
            ExecuteEmergencyRecovery();
        }
    }

    public void ExecuteEmergencyRecovery()
    {
        // 1. Ensure Windows Explorer is running as safety infrastructure
        EnsureExplorerRunning();

        // 2. Fire emergency event to desktop host
        EmergencyTriggered?.Invoke(this, EventArgs.Empty);
    }

    public static void EnsureExplorerRunning()
    {
        try
        {
            var explorerProcesses = Process.GetProcessesByName("explorer");
            if (explorerProcesses.Length == 0)
            {
                Process.Start("explorer.exe");
            }
        }
        catch { }
    }

    public static void LaunchTaskManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        }
        catch { }
    }

    public static void OpenTaskbarSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true });
        }
        catch { }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        UnregisterEmergencyHotkey();
    }
}
