using Microsoft.UI.Xaml.Data;

namespace Pausengong.App.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        !(value is true);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        !(value is true);
}
