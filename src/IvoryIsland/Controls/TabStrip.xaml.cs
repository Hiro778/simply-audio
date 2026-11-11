using System.Collections.ObjectModel;
using IvoryIsland.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace IvoryIsland.Controls;

public sealed partial class TabStrip : UserControl
{
    public event EventHandler<TabItemModel>? TabSelected;
    public event EventHandler<TabItemModel>? TabClosed;
    public event EventHandler? NewTabClicked;

    public TabStrip()
    {
        InitializeComponent();
    }

    public void BindTabs(ObservableCollection<TabItemModel> tabs)
    {
        TabsItemsControl.ItemsSource = tabs;
    }

    private void OnTabPillPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TabItemModel tab)
        {
            TabSelected?.Invoke(this, tab);
        }
    }

    private void OnTabCloseButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TabItemModel tab)
        {
            TabClosed?.Invoke(this, tab);
        }
    }

    private void OnNewTabButtonClick(object sender, RoutedEventArgs e)
    {
        NewTabClicked?.Invoke(this, EventArgs.Empty);
    }
}