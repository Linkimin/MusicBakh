using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Сравнивает Id строки (int) с editing-таргетом (int?) → Visibility. Используется в
/// PlaylistRowTemplate для инлайн-переименования: показывает TextBox только для той
/// строки, чей Id совпадает с PlaylistsViewModel.EditingPlaylistId (Task 7).
/// ConverterParameter="Invert" переворачивает логику (для обратного случая — TextBlock
/// с именем, видимый пока строка НЕ редактируется) — тот же приём, что и в
/// CountToVisibilityConverter.
/// </summary>
public sealed class IdEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool matches = values.Length >= 2 && values[0] is int id && values[1] is int editingId && id == editingId;
        bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        bool visible = invert ? !matches : matches;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
