using System.ComponentModel;
using IvoryIsland.Models;
using IvoryIsland.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IvoryIsland.Views;

public sealed partial class NowPlayingTabView : UserControl
{
    private IslandViewModel? _viewModel;
    private bool _isUpdatingUi;

    public NowPlayingTabView()
    {
        InitializeComponent();
    }

    public void Bind(IslandViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RefreshUi();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshUi();
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
            SourceIdentityTextBlock.Text = _viewModel.SourceDisplayName;

            // Artwork
            if (_viewModel.AlbumArtwork is not null)
            {
                ArtworkImage.Source = _viewModel.AlbumArtwork;
                ArtworkImage.Visibility = Visibility.Visible;
                FallbackArtIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                ArtworkImage.Source = null;
                ArtworkImage.Visibility = Visibility.Collapsed;
                FallbackArtIcon.Visibility = Visibility.Visible;
            }

            // Transport
            PlayPauseIcon.Glyph = _viewModel.PlayPauseGlyph;
            PlayPauseButton.IsEnabled = _viewModel.CanTogglePlayPause;
            PreviousButton.IsEnabled = _viewModel.CanSkipPrevious;
            NextButton.IsEnabled = _viewModel.CanSkipNext;
            ShuffleButton.IsEnabled = _viewModel.CanShuffle;
            RepeatButton.IsEnabled = _viewModel.CanRepeat;

            ShuffleIcon.Opacity = _viewModel.IsShuffleActive ? 1.0 : 0.45;
            RepeatIcon.Opacity = _viewModel.RepeatMode != "None" ? 1.0 : 0.45;

            EqualizerControl.IsPlaying = _viewModel.IsPlaying;

            // Volume
            VolumeIcon.Glyph = _viewModel.VolumeGlyph;
            if (Math.Abs(VolumeSlider.Value - _viewModel.VolumePercent) > 0.5)
            {
                VolumeSlider.Value = _viewModel.VolumePercent;
            }
            VolumePercentageTextBlock.Text = _viewModel.VolumePercentageText;

            // Session Indicators
            int count = _viewModel.SessionCount;
            SessionBadgeCountText.Text = count == 1 ? "1 Session" : $"{count} Sessions";
            SwitchSpotifyButton.Visibility = _viewModel.HasSpotifySession ? Visibility.Visible : Visibility.Collapsed;
            SwitchBrowserButton.Visibility = _viewModel.HasBrowserSession ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private async void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.TogglePlayPauseAsync();
    }

    private async void OnPreviousButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.SkipPreviousAsync();
    }

    private async void OnNextButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.SkipNextAsync();
    }

    private async void OnShuffleButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.ToggleShuffleAsync();
    }

    private async void OnRepeatButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.CycleRepeatAsync();
    }

    private void OnMuteButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleMute();
    }

    private void OnVolumeSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUi || _viewModel is null)
            return;
        _viewModel.VolumePercent = e.NewValue;
    }

    private void OnSwitchSpotifyButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.SelectSpotifyIfAvailable();
    }

    private void OnSwitchBrowserButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.SelectBrowserIfAvailable();
    }
}