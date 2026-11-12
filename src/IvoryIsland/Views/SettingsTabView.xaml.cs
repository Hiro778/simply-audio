using IvoryIsland.Models;
using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IvoryIsland.Views;

public sealed partial class SettingsTabView : UserControl
{
    private SettingsService? _settingsService;
    private WindowManager? _windowManager;
    private GlassBackdropService? _backdropService;
    private IslandViewModel? _viewModel;
    private IntPtr _windowHandle;
    private bool _isUpdatingUi;

    public SettingsTabView()
    {
        InitializeComponent();
    }

    public void Bind(
        SettingsService settingsService,
        WindowManager windowManager,
        GlassBackdropService backdropService,
        IslandViewModel viewModel,
        IntPtr windowHandle)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _backdropService = backdropService ?? throw new ArgumentNullException(nameof(backdropService));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _windowHandle = windowHandle;

        RefreshUi();
    }

    private void RefreshUi()
    {
        if (_settingsService is null)
            return;

        _isUpdatingUi = true;
        try
        {
            var cur = _settingsService.Current;
            OperatingModeComboBox.SelectedIndex = cur.OperatingMode == DesktopOperatingMode.Immersive ? 0 : 1;
            DockMagnificationCheckBox.IsChecked = cur.DockMagnification;
            ShowMediaInMenuBarCheckBox.IsChecked = cur.ShowMediaInMenuBar;
            ShowVolumeHudCheckBox.IsChecked = cur.ShowVolumeHud;

            StartWithWindowsCheckBox.IsChecked = cur.StartWithWindows;
            FullscreenAutoHideCheckBox.IsChecked = cur.AutoHideInFullscreen;
            CompactIdleCheckBox.IsChecked = cur.UseCompactIdleMode;

            int opacityPct = (int)Math.Round(cur.GlassOpacity * 100.0);
            GlassOpacitySlider.Value = opacityPct;
            GlassOpacityLabel.Text = $"{opacityPct}%";

            DataDirectoryTextBlock.Text = _settingsService.GetEffectiveDataDirectory();
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void OnOperatingModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi || _settingsService is null)
            return;

        _settingsService.Current.OperatingMode = OperatingModeComboBox.SelectedIndex == 0
            ? DesktopOperatingMode.Immersive
            : DesktopOperatingMode.Standard;
        _settingsService.Save();
    }

    private void OnDockMagnificationCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settingsService is null)
            return;
        _settingsService.Current.DockMagnification = DockMagnificationCheckBox.IsChecked == true;
        _settingsService.Save();
    }

    private void OnShowMediaInMenuBarCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settingsService is null)
            return;
        _settingsService.Current.ShowMediaInMenuBar = ShowMediaInMenuBarCheckBox.IsChecked == true;
        _settingsService.Save();
    }

    private void OnShowVolumeHudCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settingsService is null)
            return;
        _settingsService.Current.ShowVolumeHud = ShowVolumeHudCheckBox.IsChecked == true;
        _settingsService.Save();
    }

    private void OnOpenTaskbarSettingsButtonClick(object sender, RoutedEventArgs e)
    {
        EmergencyRecoveryService.OpenTaskbarSettings();
    }

    private void OnGlassOpacitySliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUi || _settingsService is null || _backdropService is null)
            return;

        double opacity = Math.Clamp(e.NewValue / 100.0, 0.65, 0.98);
        _settingsService.Current.GlassOpacity = opacity;
        _settingsService.Save();

        GlassOpacityLabel.Text = $"{(int)Math.Round(e.NewValue)}%";
        _backdropService.ApplyIvoryLiquidGlass(opacity);
    }

    private void OnStartWithWindowsCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settingsService is null)
            return;
        _settingsService.SetStartWithWindows(StartWithWindowsCheckBox.IsChecked == true);
    }

    private void OnFullscreenAutoHideCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settingsService is null)
            return;
        _settingsService.Current.AutoHideInFullscreen = FullscreenAutoHideCheckBox.IsChecked == true;
        _settingsService.Save();
        _windowManager?.EvaluateFullscreenState();
    }

    private void OnCompactIdleCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settingsService is null || _viewModel is null)
            return;
        _settingsService.Current.UseCompactIdleMode = CompactIdleCheckBox.IsChecked == true;
        _settingsService.Save();
        _viewModel.RecalculateVisualState(isExternalVolumeTrigger: false);
    }

    private void OnAnchorLeftClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null || _windowManager is null) return;
        _settingsService.Current.PositionAlignment = TopPositionAlignment.TopLeft;
        _settingsService.Current.CustomX = null;
        _settingsService.Current.CustomY = null;
        _settingsService.Save();
        _windowManager.UpdateSizeAndPosition(780, 72, preserveCustomPosition: false);
    }

    private void OnAnchorCenterClick(object sender, RoutedEventArgs e)
    {
        _windowManager?.ResetToTopCenter();
    }

    private void OnAnchorRightClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null || _windowManager is null) return;
        _settingsService.Current.PositionAlignment = TopPositionAlignment.TopRight;
        _settingsService.Current.CustomX = null;
        _settingsService.Current.CustomY = null;
        _settingsService.Save();
        _windowManager.UpdateSizeAndPosition(780, 72, preserveCustomPosition: false);
    }

    private void OnResetPositionClick(object sender, RoutedEventArgs e)
    {
        _windowManager?.ResetToTopCenter();
    }

    private async void OnBrowseDataFolderButtonClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null)
            return;

        string? picked = await _settingsService.PickCustomDataDirectoryAsync(_windowHandle);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            DataDirectoryTextBlock.Text = _settingsService.GetEffectiveDataDirectory();
        }
    }

    private void OnResetDataFolderButtonClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null)
            return;

        _settingsService.ResetDataDirectoryToDefault();
        DataDirectoryTextBlock.Text = _settingsService.GetEffectiveDataDirectory();
    }
}