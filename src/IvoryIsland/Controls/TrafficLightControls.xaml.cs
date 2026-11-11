using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace IvoryIsland.Controls;

public sealed partial class TrafficLightControls : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? MaximizeRequested;
    public event EventHandler? FullscreenRequested;

    public TrafficLightControls()
    {
        InitializeComponent();
    }

    private void OnContainerPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        CloseGlyph.Opacity = 1.0;
        MinimizeGlyph.Opacity = 1.0;
        MaximizeGlyph.Opacity = 1.0;
    }

    private void OnContainerPointerExited(object sender, PointerRoutedEventArgs e)
    {
        CloseGlyph.Opacity = 0.0;
        MinimizeGlyph.Opacity = 0.0;
        MaximizeGlyph.Opacity = 0.0;
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
    {
        MinimizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMaximizeButtonClick(object sender, RoutedEventArgs e)
    {
        MaximizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMaximizeButtonDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        FullscreenRequested?.Invoke(this, EventArgs.Empty);
    }
}