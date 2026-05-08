using MusicLibrary.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicLibrary.Converters;

/// <summary>
/// Переводит тип сообщения ViewModel в цвет текста.
/// Так интерфейс показывает успехи и ошибки без логики в XAML code-behind.
/// </summary>
public sealed class StatusKindToBrushConverter : IValueConverter
{
    public Brush InfoBrush { get; set; } = Brushes.Gray;
    public Brush SuccessBrush { get; set; } = Brushes.Green;
    public Brush ErrorBrush { get; set; } = Brushes.Red;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            OperationMessageKind.Success => SuccessBrush,
            OperationMessageKind.Error => ErrorBrush,
            _ => InfoBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
