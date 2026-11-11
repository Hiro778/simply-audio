using System.ComponentModel;
using IvoryIsland.Models;
using IvoryIsland.Services;
using IvoryIsland.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace IvoryIsland.Views;

public sealed partial class FloatingIslandView : UserControl
{
    private IslandViewModel? _viewModel;
    private WindowManager? _windowManager;
    private AnimationService? _animationService;
    private SettingsService? _settingsService;
    private GlassBackdropService? _backdropService;
    private bool _isUpdatingUi;
    private bool _isPointerDragging;

    public event EventHandler? DismissRequested;

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
        _viewModel.VisualStateChanged += OnViewModelVisualStateChanged;

        SessionsListView.ItemsSource = _viewModel.AvailableSessions;
        RefreshUi();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshUi();
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

    private void RefreshUi()
    {
        if (_viewModel is null)
            return;

        _isUpdatingUi = true;
        try
        {
            TrackTitleTextBlock.Text = _viewModel.TrackTitle;
            ArtistSubtitleTextBlock.Text = _viewModel.ArtistName;

            // Artwork
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

            // Source app identity icon
            bool isSpotify = _viewModel.IsSelectedSourceSpotify || (!_viewModel.HasActiveSession && _viewModel.HasSpotifySession);
            bool isBrowser = _viewModel.IsSelectedSourceBrowser;

            SpotifyIconGrid.Visibility = isSpotify ? Visibility.Visible : Visibility.Collapsed;
            BrowserIconGrid.Visibility = (isBrowser && !isSpotify) ? Visibility.Visible : Visibility.Collapsed;
            GenericSourceIcon.Visibility = (!isSpotify && !isBrowser && _viewModel.HasActiveSession) ? Visibility.Visible : Visibility.Collapsed;

            // Transport
            PlayPauseIcon.Glyph = _viewModel.PlayPauseGlyph;
            PlayPauseButton.IsEnabled = _viewModel.CanTogglePlayPause;
            PreviousButton.IsEnabled = _viewModel.CanSkipPrevious;
            NextButton.IsEnabled = _viewModel.CanSkipNext;
            ShuffleButton.IsEnabled = _viewModel.CanShuffle;
            RepeatButton.IsEnabled = _viewModel.CanRepeat;

            ShuffleIcon.Opacity = _viewModel.IsShuffleActive ? 1.0 : 0.45;
            RepeatIcon.Opacity = _viewModel.RepeatMode != "None" ? 1.0 : 0.45;

            // Waveform visualizer
            EqualizerControl.IsPlaying = _viewModel.IsPlaying;

            // Volume
            VolumeIcon.Glyph = _viewModel.VolumeGlyph;
            if (Math.Abs(VolumeSlider.Value - _viewModel.VolumePercent) > 0.5)
            {
                VolumeSlider.Value = _viewModel.VolumePercent;
            }
            VolumePercentageTextBlock.Text = _viewModel.VolumePercentageText;

            // Source Drawer
            SourceDrawerBorder.Visibility = _viewModel.IsSourceDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
            DrawerSessionCountLabel.Text = $"{_viewModel.SessionCount} active session(s)";
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void OnSourceDropdownToggleClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.IsSourceDrawerOpen = !_viewModel.IsSourceDrawerOpen;
        }
    }

    private void OnSessionListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MediaSessionSnapshot snapshot)
        {
            _viewModel?.SelectSource(snapshot.SessionId);
            if (_viewModel is not null) _viewModel.IsSourceDrawerOpen = false;
        }
    }

    private async void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el) _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null) await _viewModel.TogglePlayPauseAsync();
    }

    private async void OnPreviousButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el) _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null) await _viewModel.SkipPreviousAsync();
    }

    private async void OnNextButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el) _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null) await _viewModel.SkipNextAsync();
    }

    private async void OnShuffleButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el) _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null) await _viewModel.ToggleShuffleAsync();
    }

    private async void OnRepeatButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el) _animationService?.AnimateControlPressFeedback(el);
        if (_viewModel is not null) await _viewModel.CycleRepeatAsync();
    }

    private void OnMuteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el) _animationService?.AnimateControlPressFeedback(el);
        _viewModel?.ToggleMute();
    }

    private void OnVolumeSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUi || _viewModel is null)
            return;
        _viewModel.VolumePercent = e.NewValue;
    }

    private void OnDismissButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement el) _animationService?.AnimateControlPressFeedback(el);
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCapsuleDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
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