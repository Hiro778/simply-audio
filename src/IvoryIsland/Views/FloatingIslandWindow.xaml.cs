using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using Microsoft.UI.Xaml;

namespace IvoryIsland.Views;

public sealed partial class FloatingIslandWindow : Window, IDisposable
{
    private readonly WindowManager _windowManager;
    private readonly GlassBackdropService _glassBackdropService;
    private bool _isDisposed;

    public WindowManager WindowManager => _windowManager;
    public FloatingIslandView View => IslandView;

    public FloatingIslandWindow(
        IslandViewModel viewModel,
        SettingsService settingsService,
        AnimationService animationService)
    {
        InitializeComponent();

        _windowManager = new WindowManager(this, settingsService);
        _windowManager.InitializeOverlayWindow();

        _glassBackdropService = new GlassBackdropService(this);
        _glassBackdropService.ApplyIvoryLiquidGlass(settingsService.Current.GlassOpacity);

        IslandView.Bind(viewModel, _windowManager, animationService, settingsService, _glassBackdropService);
        IslandView.DismissRequested += (_, _) =>
        {
            _windowManager.SetOverlayVisible(false);
        };
    }

    public void ShowIsland()
    {
        _windowManager.SetOverlayVisible(true);
        _windowManager.UpdateSizeAndPosition(780, 72);
    }

    public void HideIsland()
    {
        _windowManager.SetOverlayVisible(false);
    }

    public void ToggleIsland()
    {
        _windowManager.ToggleVisibility();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        _glassBackdropService.Dispose();
        _windowManager.Dispose();
    }
}