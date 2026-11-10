using System.ComponentModel;
using IvoryIsland.Models;
using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace IvoryIsland.Views;

/// <summary>
/// Code-behind for the unified FloatingIslandView capsule.
/// Binds ViewModel state updates to UI elements, Composition animations, and the Windows FolderPicker
/// for user data/cache directory configuration.
/// </summary>
public sealed partial class FloatingIslandView : UserControl
{
    private IslandViewModel? _viewModel;
    private WindowManager? _windowManager;
    private AnimationService? _animationService;
    private SettingsService? _settingsService;
    private GlassBackdropService? _backdropService;
    private bool _isUpdatingUiFromVm;
    private bool _isPointerDragging;

    public FloatingIslandView()
    {
        InitializeComponent();
    }

    public void Bind(
        IslandViewModel viewModel,
        WindowManager windowManager,
        AnimationService animationService,
        SettingsService settingsService,
        GlassBackdropService backdropService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _animationService = animationService ?? throw new ArgumentNullException(nameof(animationService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _backdropService = backdropService ?? throw new ArgumentNullException(nameof(backdropService));

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.SourceSwitched += OnViewModelSourceSwitched;
        _viewModel.VisualStateChanged += OnViewModelVisualStateChanged;

        SessionsListView.ItemsSource = _viewModel.AvailableSessions;

        SyncSettingsCheckBoxes();
        RefreshAllBindings();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshAllBindings();
    }

    private void OnViewModelSourceSwitched(object? sender, EventArgs e)
    {
        _animationService?.AnimateSourceSwitchTransition(MediaInfoContainer);
    }

    private void OnViewModelVisualStateChanged(object? sender, (IslandVisualState Previous, IslandVisualState Current) e)
    {
        if (e.Current is IslandVisualState.MediaPlaying or IslandVisualState.MediaPaused or IslandVisualState.VolumeOnlyTransient)
        {
            _animationService?.AnimateCapsuleExpand(MainCapsuleBorder);
        }
        else if (e.Current == IslandVisualState.IdleHidden)
        {
            _animationService?.AnimateCapsuleCollapse(MainCapsuleBorder, 0.0f);
        }
    }

    private void RefreshAllBindings()
    {
        if (_viewModel is null)
            return;

        _isUpdatingUiFromVm = true;
        try
        {
            bool volumeOnly = _viewModel.IsVolumeOnlyCompactMode && !_viewModel.IsSourceDrawerOpen && !_viewModel.IsOptionsDrawerOpen;
            FullMediaContentGrid.Visibility = volumeOnly ? Visibility.Collapsed : Visibility.Visible;
            CompactVolumeOnlyGrid.Visibility = volumeOnly ? Visibility.Visible : Visibility.Collapsed;

            bool hasSpotify = _viewModel.HasSpotifySession;
            bool hasBrowser = _viewModel.HasBrowserSession;
            var selected = _viewModel.SelectedSession;

            if (hasSpotify || selected?.Identity.Kind == MediaSourceKind.Spotify || !_viewModel.HasActiveSession)
            {
                SpotifyQuickSourceButton.Visibility = Visibility.Visible;
                SpotifyQuickSourceButton.Opacity = (_viewModel.IsSelectedSourceSpotify || !_viewModel.HasActiveSession) ? 1.0 : 0.52;
            }
            else
            {
                SpotifyQuickSourceButton.Visibility = Visibility.Collapsed;
            }

            if (hasBrowser || selected?.Identity.IsBrowserSession == true)
            {
                BrowserQuickSourceButton.Visibility = Visibility.Visible;
                BrowserQuickSourceButton.Opacity = _viewModel.IsSelectedSourceBrowser ? 1.0 : 0.52;
            }
            else
            {
                BrowserQuickSourceButton.Visibility = Visibility.Collapsed;
            }

            if (selected is not null && !selected.Identity.IsBrowserSession && selected.Identity.Kind != MediaSourceKind.Spotify)
            {
                GenericSourceBadge.Visibility = Visibility.Visible;
                GenericSourceGlyph.Glyph = selected.Identity.GlyphCode;
            }
            else
            {
                GenericSourceBadge.Visibility = Visibility.Collapsed;
            }

            SessionCountBadge.Visibility = _viewModel.HasMultipleSessions ? Visibility.Visible : Visibility.Collapsed;
            SessionCountText.Text = _viewModel.SessionCountBadgeText;
            DrawerSessionCountLabel.Text = $"{_viewModel.SessionCount} active session(s)";

            TrackTitleTextBlock.Text = _viewModel.TrackTitle;
            ArtistSubtitleTextBlock.Text = _viewModel.ArtistName;

            if (_viewModel.AlbumArtwork is not null)
            {
                AlbumArtworkImage.Source = _viewModel.AlbumArtwork;
                AlbumArtworkImage.Visibility = Visibility.Visible;
                FallbackArtworkIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                AlbumArtworkImage.Source = null;
                AlbumArtworkImage.Visibility = Visibility.Collapsed;
                FallbackArtworkIcon.Visibility = Visibility.Visible;
            }

            double eqOpacity = _viewModel.IsPlaying ? 0.92 : 0.38;
            PlaybackEqualizerIndicator.Opacity = eqOpacity;
            EqBar1.Height = _viewModel.IsPlaying ? 9 : 4;
            EqBar2.Height = _viewModel.IsPlaying ? 15 : 6;
            EqBar3.Height = _viewModel.IsPlaying ? 11 : 5;
            EqBar4.Height = _viewModel.IsPlaying ? 7 : 4;

            PlayPauseIcon.Glyph = _viewModel.PlayPauseGlyph;
            PlayPauseButton.IsEnabled = _viewModel.CanTogglePlayPause;
            PreviousButton.IsEnabled = _viewModel.CanSkipPrevious;
            NextButton.IsEnabled = _viewModel.CanSkipNext;
            ShuffleButton.IsEnabled = _viewModel.CanShuffle;
            RepeatButton.IsEnabled = _viewModel.CanRepeat;

            ShuffleIcon.Opacity = _viewModel.IsShuffleActive ? 1.0 : 0.55;
            RepeatIcon.Opacity = _viewModel.RepeatMode != "None" ? 1.0 : 0.55;

            VolumeSpeakerIcon.Glyph = _viewModel.VolumeGlyph;
            CompactVolumeIcon.Glyph = _viewModel.VolumeGlyph;

            if (Math.Abs(SystemVolumeSlider.Value - _viewModel.VolumePercent) > 0.5)
            {
                SystemVolumeSlider.Value = _viewModel.VolumePercent;
            }
            CompactVolumeBar.Value = _viewModel.VolumePercent;
            VolumePercentageTextBlock.Text = _viewModel.VolumePercentageText;
            CompactVolumeText.Text = _viewModel.VolumePercentageText;

            bool showDrawer = _viewModel.IsSourceDrawerOpen || _viewModel.IsOptionsDrawerOpen;
            IntegratedDrawerBorder.Visibility = showDrawer ? Visibility.Visible : Visibility.Collapsed;
            SourceSelectorPanel.Visibility = _viewModel.IsSourceDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
            OptionsSettingsPanel.Visibility = _viewModel.IsOptionsDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _isUpdatingUiFromVm = false;
        }
    }

    private void SyncSettingsCheckBoxes()
    {
        if (_settingsService is null)
            return;

        _isUpdatingUiFromVm = true;
        try
        {
            var cur = _settingsService.Current;
            StartWithWindowsCheckBox.IsChecked = cur.StartWithWindows;
            FullscreenAutoHideCheckBox.IsChecked = cur.AutoHideInFullscreen;
            ManualSourceLockCheckBox.IsChecked = cur.SelectionMode == SourceSelectionMode.ManualLocked;
            CompactIdleCheckBox.IsChecked = cur.UseCompactIdleMode;
            GlassOpacitySlider.Value = Math.Round(cur.GlassOpacity * 100.0);
            DataDirectoryTextBlock.Text = _settingsService.GetEffectiveDataDirectory();
        }
        finally
        {
            _isUpdatingUiFromVm = false;
        }
    }

    private void OnSpotifyQuickSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        _viewModel?.SelectSpotifyIfAvailable();
    }

    private void OnBrowserQuickSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        _viewModel?.SelectBrowserIfAvailable();
    }

    private void OnSourceDropdownToggleClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;
        _viewModel.IsSourceDrawerOpen = !_viewModel.IsSourceDrawerOpen;
    }

    private async void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null)
            await _viewModel.TogglePlayPauseAsync();
    }

    private async void OnPreviousButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null)
            await _viewModel.SkipPreviousAsync();
    }

    private async void OnNextButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null)
            await _viewModel.SkipNextAsync();
    }

    private async void OnShuffleButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null)
            await _viewModel.ToggleShuffleAsync();
    }

    private async void OnRepeatButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null)
            await _viewModel.CycleRepeatAsync();
    }

    private void OnMuteToggleButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        _viewModel?.ToggleMute();
    }

    private void OnSystemVolumeSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUiFromVm || _viewModel is null)
            return;
        _viewModel.VolumePercent = e.NewValue;
    }

    private void OnMoreOptionsButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el)
            _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is null)
            return;

        SyncSettingsCheckBoxes();
        _viewModel.IsOptionsDrawerOpen = !_viewModel.IsOptionsDrawerOpen;
    }

    private void OnSessionListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MediaSessionSnapshot clickedSession)
        {
            _viewModel?.SelectSource(clickedSession.SessionId);
        }
    }

    private void OnStartWithWindowsCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUiFromVm || _settingsService is null)
            return;
        bool enable = StartWithWindowsCheckBox.IsChecked == true;
        _settingsService.SetStartWithWindows(enable);
    }

    private void OnFullscreenAutoHideCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUiFromVm || _settingsService is null)
            return;
        _settingsService.Current.AutoHideInFullscreen = FullscreenAutoHideCheckBox.IsChecked == true;
        _settingsService.Save();
        _windowManager?.EvaluateFullscreenState();
    }

    private void OnManualSourceLockCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUiFromVm || _settingsService is null)
            return;
        _settingsService.Current.SelectionMode = ManualSourceLockCheckBox.IsChecked == true
            ? SourceSelectionMode.ManualLocked
            : SourceSelectionMode.SmartStable;
        _settingsService.Save();
    }

    private void OnCompactIdleCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUiFromVm || _settingsService is null || _viewModel is null)
            return;
        _settingsService.Current.UseCompactIdleMode = CompactIdleCheckBox.IsChecked == true;
        _settingsService.Save();
        _viewModel.RecalculateVisualState(isExternalVolumeTrigger: false);
    }

    private void OnGlassOpacitySliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUiFromVm || _settingsService is null || _backdropService is null)
            return;

        double opacity = Math.Clamp(e.NewValue / 100.0, 0.65, 0.98);
        _settingsService.Current.GlassOpacity = opacity;
        _settingsService.Save();

        _backdropService.ApplyIvoryLiquidGlass(opacity);
        MainCapsuleBorder.Background = _backdropService.CreateCapsuleSurfaceBrush(opacity);
    }

    private async void OnBrowseDataFolderButtonClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null || _windowManager is null)
            return;

        string? picked = await _settingsService.PickCustomDataDirectoryAsync(_windowManager.WindowHandle);
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

    private void OnResetPositionButtonClick(object sender, RoutedEventArgs e)
    {
        _windowManager?.ResetToTopCenter();
    }

    private void OnAnchorLeftClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null || _windowManager is null) return;
        _settingsService.Current.PositionAlignment = TopPositionAlignment.TopLeft;
        _settingsService.Current.CustomX = null;
        _settingsService.Current.CustomY = null;
        _settingsService.Save();
        _windowManager.UpdateSizeAndPosition(780, 312, preserveCustomPosition: false);
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
        _windowManager.UpdateSizeAndPosition(780, 312, preserveCustomPosition: false);
    }

    private void OnDragHandleDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        _windowManager?.ResetToTopCenter();
    }

    private void OnDragSurfacePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
            return;

        var props = e.GetCurrentPoint(element).Properties;
        if (props.IsLeftButtonPressed)
        {
            _isPointerDragging = true;
            element.CapturePointer(e.Pointer);
            _windowManager?.BeginDrag();
        }
    }

    private void OnDragSurfacePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDragging)
        {
            _windowManager?.UpdateDrag();
        }
    }

    private void OnDragSurfacePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDragging && sender is UIElement element)
        {
            _isPointerDragging = false;
            element.ReleasePointerCapture(e.Pointer);
            _windowManager?.EndDrag();
        }
    }
}
