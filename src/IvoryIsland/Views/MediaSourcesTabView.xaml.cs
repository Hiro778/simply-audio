using IvoryIsland.Models;
using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IvoryIsland.Views;

public sealed partial class MediaSourcesTabView : UserControl
{
    private IslandViewModel? _viewModel;
    private SettingsService? _settingsService;
    private MediaSessionManager? _sessionManager;

    public MediaSourcesTabView()
    {
        InitializeComponent();
    }

    public void Bind(IslandViewModel viewModel, SettingsService settingsService, MediaSessionManager sessionManager)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));

        SessionsListView.ItemsSource = _viewModel.AvailableSessions;
        _viewModel.PropertyChanged += (_, _) => UpdateSummary();

        ManualLockCheckBox.IsChecked = _settingsService.Current.SelectionMode == SourceSelectionMode.ManualLocked;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (_viewModel is null)
            return;

        int count = _viewModel.SessionCount;
        SessionCountSummaryText.Text = $"{count} active session(s) detected via Windows GSMTC";
    }

    private async void OnRefreshSessionsButtonClick(object sender, RoutedEventArgs e)
    {
        if (_sessionManager is not null)
        {
            await _sessionManager.ReconcileSessionsAsync(isSessionListChange: true);
            UpdateSummary();
        }
    }

    private void OnSessionListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MediaSessionSnapshot snapshot)
        {
            _viewModel?.SelectSource(snapshot.SessionId);
        }
    }

    private void OnSessionSwitchButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sessionId)
        {
            _viewModel?.SelectSource(sessionId);
        }
    }

    private void OnManualLockCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null)
            return;

        _settingsService.Current.SelectionMode = ManualLockCheckBox.IsChecked == true
            ? SourceSelectionMode.ManualLocked
            : SourceSelectionMode.SmartStable;
        _settingsService.Save();
    }
}