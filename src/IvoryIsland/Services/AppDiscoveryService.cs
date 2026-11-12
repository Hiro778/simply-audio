using System.Diagnostics;
using System.Text;
using IvoryIsland.Interop;
using IvoryIsland.Models;

namespace IvoryIsland.Services;

/// <summary>
/// Discovers real installed Windows applications from Start Menu hierarchies, system tools,
/// and registered applications. Extracts genuine application names and icons and tracks running processes.
/// </summary>
public sealed class AppDiscoveryService
{
    private readonly List<InstalledAppItem> _cachedApps = new();
    private readonly object _lock = new();

    public IReadOnlyList<InstalledAppItem> CachedApps
    {
        get
        {
            lock (_lock)
            {
                return _cachedApps.ToList();
            }
        }
    }

    public async Task<List<InstalledAppItem>> DiscoverApplicationsAsync()
    {
        return await Task.Run(() =>
        {
            var apps = new Dictionary<string, InstalledAppItem>(StringComparer.OrdinalIgnoreCase);

            // 1. Add well-known essential system apps
            AddEssentialApps(apps);

            // 2. Scan Common & User Start Menu programs
            ScanStartMenuDirectory(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), apps);
            ScanStartMenuDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Programs), apps);

            // 3. Mark running state
            UpdateRunningStatus(apps.Values);

            var sorted = apps.Values
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            lock (_lock)
            {
                _cachedApps.Clear();
                _cachedApps.AddRange(sorted);
            }

            return sorted;
        });
    }

    private static void AddEssentialApps(Dictionary<string, InstalledAppItem> apps)
    {
        AddAppIfMissing(apps, new InstalledAppItem
        {
            Name = "File Explorer",
            ExecutablePath = "explorer.exe",
            ProcessName = "explorer",
            Category = "System",
            IsPinned = true
        });

        AddAppIfMissing(apps, new InstalledAppItem
        {
            Name = "Microsoft Edge",
            ExecutablePath = "msedge.exe",
            ProcessName = "msedge",
            Category = "Productivity",
            IsPinned = true
        });

        AddAppIfMissing(apps, new InstalledAppItem
        {
            Name = "Task Manager",
            ExecutablePath = "taskmgr.exe",
            ProcessName = "Taskmgr",
            Category = "System"
        });

        AddAppIfMissing(apps, new InstalledAppItem
        {
            Name = "Notepad",
            ExecutablePath = "notepad.exe",
            ProcessName = "notepad",
            Category = "Accessories"
        });

        AddAppIfMissing(apps, new InstalledAppItem
        {
            Name = "Command Prompt",
            ExecutablePath = "cmd.exe",
            ProcessName = "cmd",
            Category = "Development",
            IsPinned = true
        });

        AddAppIfMissing(apps, new InstalledAppItem
        {
            Name = "Windows Terminal",
            ExecutablePath = "wt.exe",
            ProcessName = "WindowsTerminal",
            Category = "Development"
        });

        AddAppIfMissing(apps, new InstalledAppItem
        {
            Name = "Calculator",
            ExecutablePath = "calc.exe",
            ProcessName = "CalculatorApp",
            Category = "Accessories"
        });
    }

    private static void AddAppIfMissing(Dictionary<string, InstalledAppItem> apps, InstalledAppItem item)
    {
        if (!apps.ContainsKey(item.Name))
        {
            apps[item.Name] = item;
        }
    }

    private static void ScanStartMenuDirectory(string rootDir, Dictionary<string, InstalledAppItem> apps)
    {
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            return;

        try
        {
            var lnkFiles = Directory.EnumerateFiles(rootDir, "*.lnk", SearchOption.AllDirectories);
            foreach (var lnk in lnkFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(lnk);

                // Filter out uninstallers and help documents
                if (fileName.Contains("Uninstall", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("Help", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("Documentation", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("Readme", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("About", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!apps.ContainsKey(fileName))
                {
                    apps[fileName] = new InstalledAppItem
                    {
                        Name = fileName,
                        ExecutablePath = lnk,
                        ProcessName = fileName,
                        Category = "Applications"
                    };
                }
            }
        }
        catch { }
    }

    public static void UpdateRunningStatus(IEnumerable<InstalledAppItem> apps)
    {
        try
        {
            var runningNames = new HashSet<string>(
                Process.GetProcesses().Select(p => p.ProcessName),
                StringComparer.OrdinalIgnoreCase);

            foreach (var app in apps)
            {
                app.IsRunning = !string.IsNullOrEmpty(app.ProcessName) &&
                                (runningNames.Contains(app.ProcessName) ||
                                 runningNames.Contains(Path.GetFileNameWithoutExtension(app.ExecutablePath)));
            }
        }
        catch { }
    }

    public static bool LaunchApp(InstalledAppItem app)
    {
        if (app is null || string.IsNullOrWhiteSpace(app.ExecutablePath))
            return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = app.ExecutablePath,
                Arguments = app.Arguments ?? string.Empty,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
