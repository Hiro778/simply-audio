namespace IvoryIsland.Models;

public enum DesktopOperatingMode
{
    Standard,
    Immersive
}

public sealed class InstalledAppItem
{
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? IconPath { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public bool IsRunning { get; set; }
    public bool IsPinned { get; set; }
}

public sealed class WindowOverviewItem
{
    public IntPtr Hwnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public bool IsMinimized { get; set; }
}

public sealed class SystemStatusSnapshot
{
    public int BatteryPercentage { get; set; } = 100;
    public bool IsBatteryCharging { get; set; }
    public bool HasBattery { get; set; }
    public string BatteryGlyph => HasBattery
        ? (IsBatteryCharging ? "\uEA93" : (BatteryPercentage > 80 ? "\uEBAA" : (BatteryPercentage > 50 ? "\uEBA7" : (BatteryPercentage > 20 ? "\uEBA4" : "\uEBA0"))))
        : "\uE7E8";

    public string WifiSsid { get; set; } = "Wi-Fi";
    public bool IsWifiConnected { get; set; } = true;
    public string WifiGlyph => IsWifiConnected ? "\uE701" : "\uEB55";

    public bool IsBluetoothEnabled { get; set; } = true;
    public string BluetoothGlyph => IsBluetoothEnabled ? "\uE702" : "\uE704";

    public int DisplayBrightness { get; set; } = 100;
    public float Volume { get; set; } = 50f;
    public bool IsMuted { get; set; }
    public string VolumeGlyph => IsMuted ? "\uE74F" : (Volume > 65f ? "\uE995" : (Volume > 30f ? "\uE994" : (Volume > 0f ? "\uE993" : "\uE74F")));

    public string FormattedTime { get; set; } = string.Empty;
    public string FormattedDate { get; set; } = string.Empty;
}
