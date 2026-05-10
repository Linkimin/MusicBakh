using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Конвертер: null / служебные значения WPF -> false, всё остальное -> true.
/// Используется для отключения seek-слайдера, когда нет играющего трека.
/// </summary>
public sealed class NullToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null
            && value != DependencyProperty.UnsetValue
            && value != Binding.DoNothing;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
