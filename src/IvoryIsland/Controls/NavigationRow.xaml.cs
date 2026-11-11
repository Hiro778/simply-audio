using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IvoryIsland.Controls;

public sealed partial class NavigationRow : UserControl
{
    public event EventHandler? BackClicked;
    public event EventHandler? ForwardClicked;
    public event EventHandler? RefreshClicked;
    public event EventHandler? ToggleFloatingControllerClicked;
    public event EventHandler? ToggleMuteClicked;
    public event EventHandler? MoreMenuClicked;

    public NavigationRow()
    {
        InitializeComponent();
    }

    public void SetAddress(string address)
    {
        AddressTextBlock.Text = address;
    }

    public void SetStreamStatus(string status)
    {
        StreamStatusTextBlock.Text = status;
    }

    public void SetMuteState(bool isMuted, string volumeGlyph)
    {
        QuickMuteIcon.Glyph = volumeGlyph;
        QuickMuteIcon.Foreground = isMuted
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["IvorySecondaryTextBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["IvoryPrimaryTextBrush"];
    }

    public void SetNavigationState(bool canGoBack, bool canGoForward)
    {
        BackButton.IsEnabled = canGoBack;
        BackButton.Opacity = canGoBack ? 1.0 : 0.4;
        ForwardButton.IsEnabled = canGoForward;
        ForwardButton.Opacity = canGoForward ? 1.0 : 0.4;
    }

    private void OnBackButtonClick(object sender, RoutedEventArgs e) => BackClicked?.Invoke(this, EventArgs.Empty);
    private void OnForwardButtonClick(object sender, RoutedEventArgs e) => ForwardClicked?.Invoke(this, EventArgs.Empty);
    private void OnRefreshButtonClick(object sender, RoutedEventArgs e) => RefreshClicked?.Invoke(this, EventArgs.Empty);
    private void OnToggleFloatingControllerClick(object sender, RoutedEventArgs e) => ToggleFloatingControllerClicked?.Invoke(this, EventArgs.Empty);
    private void OnQuickMuteButtonClick(object sender, RoutedEventArgs e) => ToggleMuteClicked?.Invoke(this, EventArgs.Empty);
    private void OnMoreMenuButtonClick(object sender, RoutedEventArgs e) => MoreMenuClicked?.Invoke(this, EventArgs.Empty);
}