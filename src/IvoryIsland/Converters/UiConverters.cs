using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IvoryIsland.Converters;

public sealed class TabBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush InactiveBrush = new(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is true) ? ActiveBrush : InactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public sealed class TabBorderConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBorder = new(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush InactiveBorder = new(Color.FromArgb(0x33, 0xCB, 0xD5, 0xE1));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is true) ? ActiveBorder : InactiveBorder;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public sealed class TabTextBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveText = new(Color.FromArgb(0xFF, 0x20, 0x23, 0x33));
    private static readonly SolidColorBrush InactiveText = new(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is true) ? ActiveText : InactiveText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public sealed class TabFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is true) ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}