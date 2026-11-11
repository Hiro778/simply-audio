using System.ComponentModel;
using IvoryIsland.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IvoryIsland.Views;

public sealed partial class AudioControlTabView : UserControl
{
    private IslandViewModel? _viewModel;
    private bool _isUpdatingUi;

    public AudioControlTabView()
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
        if (e.PropertyName is nameof(IslandViewModel.VolumePercent) or
            nameof(IslandViewModel.IsMuted) or
            nameof(IslandViewModel.VolumeGlyph) or
            nameof(IslandViewModel.VolumePercentageText))
        {
            RefreshUi();
        }
    }

    private void RefreshUi()
    {
        if (_viewModel is null)
            return;

        _isUpdatingUi = true;
        try
        {
            if (Math.Abs(MasterVolumeSlider.Value - _viewModel.VolumePercent) > 0.5)
            {
                MasterVolumeSlider.Value = _viewModel.VolumePercent;
            }
            VolumePercentageLabel.Text = _viewModel.VolumePercentageText;

            MuteStatusIcon.Glyph = _viewModel.VolumeGlyph;
            MuteStatusText.Text = _viewModel.IsMuted ? "Muted" : "Active";
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void OnMasterVolumeSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUi || _viewModel is null)
            return;
        _viewModel.VolumePercent = e.NewValue;
    }

    private void OnMuteMasterButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleMute();
    }

    private void OnPresetMuteClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && !_viewModel.IsMuted)
            _viewModel.ToggleMute();
    }

    private void OnPreset25Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            if (_viewModel.IsMuted) _viewModel.ToggleMute();
            _viewModel.VolumePercent = 25.0;
        }
    }

    private void OnPreset50Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            if (_viewModel.IsMuted) _viewModel.ToggleMute();
            _viewModel.VolumePercent = 50.0;
        }
    }

    private void OnPreset75Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            if (_viewModel.IsMuted) _viewModel.ToggleMute();
            _viewModel.VolumePercent = 75.0;
        }
    }

    private void OnPreset100Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            if (_viewModel.IsMuted) _viewModel.ToggleMute();
            _viewModel.VolumePercent = 100.0;
        }
    }
}