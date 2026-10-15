using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// int (0-based ItemsControl.AlternationIndex) → человекочитаемый номер трека (1-based).
/// Используется в PlaylistDetailTemplate: номер строки в списке треков плейлиста.
/// </summary>
public sealed class IndexToTrackNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int index ? (index + 1).ToString(culture) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
