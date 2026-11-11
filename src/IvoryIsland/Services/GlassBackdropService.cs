using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT;

namespace IvoryIsland.Services;

/// <summary>
/// Configures the Minimal Ivory Liquid Glass backdrop (#F7F5EF) across Windows 11 and Windows 10 (1809+).
/// Uses native DesktopAcrylicController where supported and falls back cleanly to a translucent ivory
/// surface without crashing or modifying undocumented registry keys.
/// </summary>
public sealed class GlassBackdropService : IDisposable
{
    private readonly Window _window;
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _backdropConfig;
    private bool _isBackdropActive;
    private bool _isDisposed;

    // White Frosted Glass palette constants (macOS-inspired pearl white)
    public static readonly Color IvoryGlassTint = Color.FromArgb(0xFF, 0xFD, 0xFC, 0xFE);
    public static readonly Color IvoryGlassSecondary = Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC);

    public bool IsNativeAcrylicActive => _isBackdropActive;

    public GlassBackdropService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public void ApplyIvoryLiquidGlass(double opacity = 0.90)
    {
        if (_isDisposed)
            return;

        float clampedOpacity = (float)Math.Clamp(opacity, 0.65, 0.98);

        try
        {
            if (DesktopAcrylicController.IsSupported())
            {
                _backdropConfig ??= new SystemBackdropConfiguration
                {
                    IsInputActive = true,
                    Theme = SystemBackdropTheme.Light
                };

                if (_acrylicController is null)
                {
                    _acrylicController = new DesktopAcrylicController();
                    var target = _window.As<ICompositionSupportsSystemBackdrop>();
                    _acrylicController.AddSystemBackdropTarget(target);
                    _acrylicController.SetSystemBackdropConfiguration(_backdropConfig);
                }

                _acrylicController.TintColor = IvoryGlassTint;
                _acrylicController.FallbackColor = IvoryGlassTint;
                _acrylicController.TintOpacity = clampedOpacity;
                _acrylicController.LuminosityOpacity = 0.93f;

                _isBackdropActive = true;
                return;
            }
        }
        catch
        {
            // Fallback cleanly on systems where composition backdrop initialization is restricted
            _isBackdropActive = false;
        }
    }

    public SolidColorBrush CreateCapsuleSurfaceBrush(double opacity = 0.90)
    {
        double effectiveAlpha = _isBackdropActive
            ? Math.Clamp(opacity * 0.84, 0.60, 0.92)
            : Math.Clamp(opacity, 0.78, 0.97);

        byte alphaByte = (byte)Math.Round(effectiveAlpha * 255.0);
        return new SolidColorBrush(Color.FromArgb(alphaByte, 0xFD, 0xFC, 0xFE));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        try
        {
            _acrylicController?.Dispose();
            _acrylicController = null;
            _backdropConfig = null;
        }
        catch
        {
        }
    }
}
