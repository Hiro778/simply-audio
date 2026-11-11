using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace IvoryIsland.Controls;

public sealed partial class EqualizerBarsControl : UserControl
{
    private readonly DispatcherQueueTimer _animationTimer;
    private readonly Rectangle[] _bars;
    private readonly double[] _idleHeights = [4, 6, 8, 5, 9, 6, 10, 7, 5, 8, 4, 6];
    private readonly double[] _targetHeights;
    private readonly Random _random = new(42);
    private bool _isPlaying;

    public EqualizerBarsControl()
    {
        InitializeComponent();

        _bars = [Bar0, Bar1, Bar2, Bar3, Bar4, Bar5, Bar6, Bar7, Bar8, Bar9, Bar10, Bar11];
        _targetHeights = new double[_bars.Length];
        Array.Copy(_idleHeights, _targetHeights, _bars.Length);

        _animationTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(120);
        _animationTimer.Tick += OnAnimationTick;

        Loaded += (_, _) => ApplyState();
        Unloaded += (_, _) => _animationTimer.Stop();
    }

    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(
            nameof(IsPlaying),
            typeof(bool),
            typeof(EqualizerBarsControl),
            new PropertyMetadata(false, (d, e) => ((EqualizerBarsControl)d).OnIsPlayingChanged((bool)e.NewValue)));

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    private void OnIsPlayingChanged(bool isPlaying)
    {
        _isPlaying = isPlaying;
        ApplyState();
    }

    private void ApplyState()
    {
        if (_isPlaying)
        {
            BarsContainer.Opacity = 1.0;
            if (!_animationTimer.IsRunning)
            {
                _animationTimer.Start();
            }
        }
        else
        {
            BarsContainer.Opacity = 0.45;
            _animationTimer.Stop();
            for (int i = 0; i < _bars.Length; i++)
            {
                _bars[i].Height = _idleHeights[i];
            }
        }
    }

    private void OnAnimationTick(DispatcherQueueTimer sender, object args)
    {
        if (!_isPlaying)
        {
            sender.Stop();
            return;
        }

        // Generate gentle organic bar heights between 4 and 22 px
        for (int i = 0; i < _bars.Length; i++)
        {
            double h = 4.0 + _random.NextDouble() * 18.0;
            _bars[i].Height = Math.Round(h, 1);
        }
    }
}