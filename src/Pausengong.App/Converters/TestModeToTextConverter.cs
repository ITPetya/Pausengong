using Microsoft.UI.Xaml.Data;

namespace Pausengong.App.Converters;

public sealed class TestModeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true
            ? "TESTMODUS AKTIV — bei Pausenzeit erscheint nur eine Benachrichtigung, kein echter Gong."
            : "Testmodus AUS — bei Pausenzeit spielt der echte Gong.";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
