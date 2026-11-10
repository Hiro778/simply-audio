using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IvoryIsland.Interop;
using IvoryIsland.Models;
using Microsoft.Win32;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace IvoryIsland.Services;

/// <summary>
/// Manages user settings persistence and data storage separation.
/// - Default user data directory: %LOCALAPPDATA%\IvoryIsland (never writes to Program Files or binary directory).
/// - Allows the user to choose a custom data/cache/export directory via native Windows FolderPicker.
/// - Gracefully recovers if a custom drive or folder becomes missing, read-only, or inaccessible.
/// </summary>
public sealed class SettingsService
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "IvoryIsland";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _defaultAppDataDir;
    private readonly string _primarySettingsFilePath;
    private readonly object _ioLock = new();

    public AppSettings Current { get; private set; } = new();

    public string DefaultDataDirectory => _defaultAppDataDir;

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService(string? customSettingsFilePath = null)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        _defaultAppDataDir = Path.Combine(localAppData, "IvoryIsland");
        TryEnsureDirectoryWritable(_defaultAppDataDir);

        if (!string.IsNullOrWhiteSpace(customSettingsFilePath))
        {
            _primarySettingsFilePath = customSettingsFilePath;
        }
        else
        {
            _primarySettingsFilePath = Path.Combine(_defaultAppDataDir, "settings.json");
        }
    }

    /// <summary>
    /// Returns the active, verified-writable user data directory.
    /// If the user configured a CustomDataDirectory on another drive (e.g. D:\IvoryIslandData)
    /// and that drive is currently available and writable, returns it; otherwise transparently
    /// falls back to %LOCALAPPDATA%\IvoryIsland without throwing.
    /// </summary>
    public string GetEffectiveDataDirectory()
    {
        string? customDir = Current.CustomDataDirectory;
        if (!string.IsNullOrWhiteSpace(customDir) && TryEnsureDirectoryWritable(customDir))
        {
            return customDir;
        }

        TryEnsureDirectoryWritable(_defaultAppDataDir);
        return _defaultAppDataDir;
    }

    public string GetArtworkCacheDirectory()
    {
        string cacheDir = Path.Combine(GetEffectiveDataDirectory(), "Cache", "Artwork");
        if (TryEnsureDirectoryWritable(cacheDir))
            return cacheDir;

        return _defaultAppDataDir;
    }

    public void Load()
    {
        lock (_ioLock)
        {
            try
            {
                if (File.Exists(_primarySettingsFilePath))
                {
                    string json = File.ReadAllText(_primarySettingsFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded is not null)
                    {
                        loaded.GlassOpacity = Math.Clamp(loaded.GlassOpacity, 0.65, 0.98);
                        loaded.TopMarginPx = Math.Clamp(loaded.TopMarginPx, 10, 48);

                        // If CustomDataDirectory is set, also check if an updated settings.json exists there
                        if (!string.IsNullOrWhiteSpace(loaded.CustomDataDirectory) &&
                            TryEnsureDirectoryWritable(loaded.CustomDataDirectory))
                        {
                            string customSettingsFile = Path.Combine(loaded.CustomDataDirectory, "settings.json");
                            if (File.Exists(customSettingsFile))
                            {
                                try
                                {
                                    var customLoaded = JsonSerializer.Deserialize<AppSettings>(
                                        File.ReadAllText(customSettingsFile), JsonOptions);
                                    if (customLoaded is not null)
                                    {
                                        customLoaded.CustomDataDirectory = loaded.CustomDataDirectory;
                                        loaded = customLoaded;
                                    }
                                }
                                catch
                                {
                                    // Keep primary settings if secondary file is corrupted
                                }
                            }
                        }

                        Current = loaded;
                    }
                }
            }
            catch
            {
                Current = new AppSettings();
            }

            Current.StartWithWindows = IsStartupRegisteredInRegistry();
        }
    }

    public void Save()
    {
        lock (_ioLock)
        {
            string json;
            try
            {
                json = JsonSerializer.Serialize(Current, JsonOptions);
            }
            catch
            {
                return;
            }

            // 1. Always persist to %LOCALAPPDATA%\IvoryIsland\settings.json so the app can locate
            // the user's chosen CustomDataDirectory on next launch without asking again.
            TryWriteFileSafe(_primarySettingsFilePath, json);

            // 2. If a custom data directory is configured and accessible, mirror settings.json there as well.
            if (!string.IsNullOrWhiteSpace(Current.CustomDataDirectory) &&
                !string.Equals(Current.CustomDataDirectory, _defaultAppDataDir, StringComparison.OrdinalIgnoreCase) &&
                TryEnsureDirectoryWritable(Current.CustomDataDirectory))
            {
                string customSettingsPath = Path.Combine(Current.CustomDataDirectory, "settings.json");
                TryWriteFileSafe(customSettingsPath, json);
            }
        }

        SettingsChanged?.Invoke(this, Current);
    }

    /// <summary>
    /// Opens a native Windows FolderPicker (with Win32 SHBrowseForFolder fallback) so the user can choose
    /// where Ivory Island stores its cache, exported settings, and diagnostic files.
    /// Persists the chosen directory immediately.
    /// </summary>
    public async Task<string?> PickCustomDataDirectoryAsync(IntPtr ownerHwnd)
    {
        string? chosenPath = null;

        try
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");

            if (ownerHwnd != IntPtr.Zero)
            {
                InitializeWithWindow.Initialize(picker, ownerHwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null && !string.IsNullOrWhiteSpace(folder.Path))
            {
                chosenPath = folder.Path;
            }
        }
        catch
        {
            // Fallback to classic Win32 SHBrowseForFolderW if WinRT FolderPicker is unavailable on a custom OS build
            chosenPath = TryPickFolderWin32Fallback(ownerHwnd);
        }

        if (string.IsNullOrWhiteSpace(chosenPath))
            return null;

        if (!TryEnsureDirectoryWritable(chosenPath))
            return null;

        Current.CustomDataDirectory = chosenPath;
        Save();
        return chosenPath;
    }

    public void ResetDataDirectoryToDefault()
    {
        Current.CustomDataDirectory = null;
        Save();
    }

    private static string? TryPickFolderWin32Fallback(IntPtr ownerHwnd)
    {
        IntPtr pidl = IntPtr.Zero;
        IntPtr displayNamePtr = Marshal.AllocHGlobal(260 * sizeof(char));
        try
        {
            var bi = new NativeMethods.BROWSEINFO
            {
                hwndOwner = ownerHwnd,
                pidlRoot = IntPtr.Zero,
                pszDisplayName = displayNamePtr,
                lpszTitle = "Select Ivory Island Data & Cache Folder:",
                ulFlags = NativeMethods.BIF_RETURNONLYFSDIRS | NativeMethods.BIF_NEWDIALOGSTYLE
            };

            pidl = NativeMethods.SHBrowseForFolder(ref bi);
            if (pidl == IntPtr.Zero)
                return null;

            var sb = new StringBuilder(260);
            if (NativeMethods.SHGetPathFromIDList(pidl, sb) && sb.Length > 0)
            {
                return sb.ToString();
            }
        }
        catch
        {
        }
        finally
        {
            Marshal.FreeHGlobal(displayNamePtr);
            if (pidl != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pidl);
        }

        return null;
    }

    public static bool TryEnsureDirectoryWritable(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return false;

        try
        {
            string? root = Path.GetPathRoot(directoryPath);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                    return false;
            }

            Directory.CreateDirectory(directoryPath);

            // Verify write permissions with an atomic probe file
            string probeFile = Path.Combine(directoryPath, $".ivory_write_probe_{Environment.ProcessId}.tmp");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryWriteFileSafe(string filePath, string content)
    {
        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, content, Encoding.UTF8);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
        }
    }

    public bool SetStartWithWindows(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true);
            if (key is null)
                return false;

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                    return false;

                key.SetValue(RunValueName, $"\"{exePath}\"");
                Current.StartWithWindows = true;
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
                Current.StartWithWindows = false;
            }

            Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsStartupRegisteredInRegistry()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
            return key?.GetValue(RunValueName) is string val && !string.IsNullOrWhiteSpace(val);
        }
        catch
        {
            return false;
        }
    }
}
