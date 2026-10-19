using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Pausengong.App.Converters;

/// <summary>Orange while test mode is active (safe — no real audio), red once it's off (real gong will play).</summary>
public sealed class TestModeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isTestMode = value is bool b && b;
        return new SolidColorBrush(isTestMode ? ColorHelper.FromArgb(255, 217, 119, 6) : ColorHelper.FromArgb(255, 185, 28, 28));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
