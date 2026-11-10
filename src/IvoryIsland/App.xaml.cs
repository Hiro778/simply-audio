using Microsoft.UI.Xaml;

namespace IvoryIsland;

/// <summary>
/// Application entry point with single-instance enforcement so multiple floating media capsules
/// can never be spawned simultaneously.
/// </summary>
public sealed partial class App : Application
{
    private const string SingleInstanceMutexName = @"Global\IvoryIsland_FloatingMediaController_v1";
    private Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnAppUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool createdNew;
        try
        {
            _singleInstanceMutex = new Mutex( initiallyOwned: true, SingleInstanceMutexName, out createdNew);
        }
        catch
        {
            // Fallback to session-local mutex if Global namespace is restricted
            _singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\IvoryIsland_FloatingMediaController_v1", out createdNew);
        }

        if (!createdNew)
        {
            // Another instance of Ivory Island is already running; exit cleanly to guarantee a single unified island.
            Environment.Exit(0);
            return;
        }

        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }

    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Prevent transient WinRT/COM notification race conditions from crashing the overlay
        e.Handled = true;
    }
}
