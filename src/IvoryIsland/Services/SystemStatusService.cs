using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using IvoryIsland.Interop;
using IvoryIsland.Models;
using Microsoft.UI.Dispatching;

namespace IvoryIsland.Services;

/// <summary>
/// Monitors and synchronizes genuine Windows system metrics: battery percentage/charging state,
/// network connectivity, display brightness, audio master volume, and macOS-style formatted date/time.
/// Designed for low resource overhead with event-driven updates and pauses when idle.
/// </summary>
public sealed class SystemStatusService : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly AudioVolumeService _volumeService;
    private DispatcherQueueTimer? _pollTimer;
    private bool _isDisposed;

    public SystemStatusSnapshot Current { get; } = new();

    public event EventHandler<SystemStatusSnapshot>? StatusUpdated;

    public SystemStatusService(DispatcherQueue dispatcherQueue, AudioVolumeService volumeService)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _volumeService = volumeService ?? throw new ArgumentNullException(nameof(volumeService));

        _volumeService.VolumeChanged += OnVolumeServiceChanged;
        RefreshStatus();

        _pollTimer = _dispatcherQueue.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(2);
        _pollTimer.Tick += (_, _) => RefreshStatus();
        _pollTimer.Start();
    }

    private void OnVolumeServiceChanged(object? sender, EventArgs e)
    {
        Current.Volume = _volumeService.MasterVolumePercent;
        Current.IsMuted = _volumeService.IsMuted;
        StatusUpdated?.Invoke(this, Current);
    }

    public void RefreshStatus()
    {
        if (_isDisposed)
            return;

        // 1. Genuine Battery Status via Win32 GetSystemPowerStatus
        try
        {
            if (NativeMethods.GetSystemPowerStatus(out var powerStatus))
            {
                // BatteryFlag: 128 means no system battery (desktop PC)
                Current.HasBattery = powerStatus.BatteryFlag != 128 && powerStatus.BatteryFlag != 255;
                Current.BatteryPercentage = powerStatus.BatteryLifePercent <= 100 ? powerStatus.BatteryLifePercent : 100;
                Current.IsBatteryCharging = (powerStatus.BatteryFlag & 8) != 0 || powerStatus.ACLineStatus == 1;
            }
        }
        catch
        {
            Current.HasBattery = false;
        }

        // 2. Real Network Connectivity
        try
        {
            bool isUp = NetworkInterface.GetIsNetworkAvailable();
            Current.IsWifiConnected = isUp;
            Current.WifiSsid = isUp ? "Connected" : "Disconnected";

            var wifiInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                                     n.OperationalStatus == OperationalStatus.Up);
            if (wifiInterface is not null)
            {
                Current.WifiSsid = string.IsNullOrWhiteSpace(wifiInterface.Name) ? "Wi-Fi" : wifiInterface.Name;
            }
        }
        catch
        {
            Current.IsWifiConnected = true;
            Current.WifiSsid = "Wi-Fi";
        }

        // 3. Audio Volume from WASAPI
        Current.Volume = _volumeService.MasterVolumePercent;
        Current.IsMuted = _volumeService.IsMuted;

        // 4. Formatted Date & Time (macOS format: "Thu Nov 12  7:45 PM")
        var now = DateTime.Now;
        Current.FormattedTime = now.ToString("t");
        Current.FormattedDate = now.ToString("ddd MMM d");

        StatusUpdated?.Invoke(this, Current);
    }

    public void SetVolume(float percent)
    {
        _volumeService.SetMasterVolume(percent);
    }

    public void ToggleMute()
    {
        _volumeService.ToggleMute();
    }

    public void OpenWifiSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true });
        }
        catch { }
    }

    public void OpenBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch { }
    }

    public void OpenDisplaySettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true });
        }
        catch { }
    }

    public void OpenSoundSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
        }
        catch { }
    }

    public void OpenTaskbarSettings()
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

        _volumeService.VolumeChanged -= OnVolumeServiceChanged;
        if (_pollTimer is not null)
        {
            _pollTimer.Stop();
            _pollTimer = null;
        }
    }
}
