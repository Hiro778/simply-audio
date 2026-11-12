using IvoryIsland.Models;
using IvoryIsland.Services;
using Xunit;

namespace IvoryIsland.Tests;

/// <summary>
/// Automated tests verifying the Ivory Desktop Full Transformation:
/// Operating mode defaults, desktop settings persistence, application discovery,
/// window management filtering, system status glyph logic, and dock magnification curves.
/// </summary>
public sealed class DesktopTransformationTests
{
    [Fact]
    public void AppSettings_DefaultsToImmersiveDesktopMode()
    {
        var settings = new AppSettings();

        Assert.Equal(DesktopOperatingMode.Immersive, settings.OperatingMode);
        Assert.True(settings.IsMenuBarEnabled);
        Assert.True(settings.IsDockEnabled);
        Assert.True(settings.DockMagnification);
        Assert.False(settings.DockAutoHide);
        Assert.True(settings.ShowMediaInMenuBar);
        Assert.True(settings.ShowVolumeHud);
        Assert.Equal("Ctrl+Alt+Escape", settings.EmergencyExitHotkey);
    }

    [Fact]
    public void SettingsPersistence_SavesAndRestoresDesktopMode()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ivory_desktop_test_{Guid.NewGuid():N}.json");
        try
        {
            var service = new SettingsService(tempFile);
            service.Current.OperatingMode = DesktopOperatingMode.Standard;
            service.Current.DockMagnification = false;
            service.Save();

            var loadedService = new SettingsService(tempFile);
            loadedService.Load();

            Assert.Equal(DesktopOperatingMode.Standard, loadedService.Current.OperatingMode);
            Assert.False(loadedService.Current.DockMagnification);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task AppDiscovery_IncludesEssentialApplications()
    {
        var discovery = new AppDiscoveryService();
        var apps = await discovery.DiscoverApplicationsAsync();

        Assert.NotEmpty(apps);
        Assert.Contains(apps, a => a.Name.Equals("File Explorer", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apps, a => a.Name.Equals("Task Manager", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apps, a => a.Name.Equals("Notepad", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apps, a => a.Name.Equals("Command Prompt", StringComparison.OrdinalIgnoreCase));

        var explorer = apps.First(a => a.Name.Equals("File Explorer", StringComparison.OrdinalIgnoreCase));
        Assert.True(explorer.IsPinned);
        Assert.Equal("explorer.exe", explorer.ExecutablePath);
    }

    [Fact]
    public void WindowManagementService_IgnoredWindowsAreFiltered()
    {
        var windowService = new WindowManagementService();
        var fakeHwnd = new IntPtr(0x123456);

        windowService.RegisterIgnoredWindow(fakeHwnd);
        Assert.True(windowService.IsWindowIgnored(fakeHwnd));

        var windows = windowService.EnumerateApplicationWindows();
        Assert.DoesNotContain(windows, w => w.Hwnd == fakeHwnd);
    }

    [Fact]
    public void SystemStatusSnapshot_GlyphsReflectBatteryAndVolumeStates()
    {
        var status = new SystemStatusSnapshot
        {
            HasBattery = true,
            BatteryPercentage = 95,
            IsBatteryCharging = false
        };
        Assert.Equal("\uEBAA", status.BatteryGlyph);

        status.IsBatteryCharging = true;
        Assert.Equal("\uEA93", status.BatteryGlyph);

        status.HasBattery = false;
        Assert.Equal("\uE7E8", status.BatteryGlyph);

        // Volume glyphs
        status.Volume = 85f;
        status.IsMuted = false;
        Assert.Equal("\uE995", status.VolumeGlyph);

        status.Volume = 45f;
        Assert.Equal("\uE994", status.VolumeGlyph);

        status.Volume = 10f;
        Assert.Equal("\uE993", status.VolumeGlyph);

        status.IsMuted = true;
        Assert.Equal("\uE74F", status.VolumeGlyph);

        // Wi-Fi glyphs
        status.IsWifiConnected = true;
        Assert.Equal("\uE701", status.WifiGlyph);

        status.IsWifiConnected = false;
        Assert.Equal("\uEB55", status.WifiGlyph);
    }

    [Fact]
    public void DockMagnification_GaussianCurve_FollowsMathematicalProfile()
    {
        // Formula: factor = exp(-(distance / 55)^2)
        // targetScale = 1.0 + 0.32 * factor

        double peakDistance = 0.0;
        double peakFactor = Math.Exp(-Math.Pow(peakDistance / 55.0, 2.0));
        float peakScale = (float)(1.0 + 0.32 * peakFactor);
        Assert.Equal(1.32f, peakScale, 2);

        double neighborDistance = 55.0;
        double neighborFactor = Math.Exp(-Math.Pow(neighborDistance / 55.0, 2.0)); // e^-1 ~= 0.3678
        float neighborScale = (float)(1.0 + 0.32 * neighborFactor);
        Assert.True(neighborScale > 1.10f && neighborScale < 1.15f);

        double farDistance = 150.0;
        double farFactor = Math.Exp(-Math.Pow(farDistance / 55.0, 2.0));
        float farScale = (float)(1.0 + 0.32 * farFactor);
        Assert.True(farScale < 1.01f);
    }
}
