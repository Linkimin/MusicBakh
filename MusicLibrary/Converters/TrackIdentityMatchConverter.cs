using MusicLibrary.Models;
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Compares the track rendered by a row with the ViewModel's PlayingTrack.
/// The comparison uses Id so copied Track objects still highlight consistently.
/// </summary>
public sealed class TrackIdentityMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not Track currentTrack || values[1] is not Track playingTrack)
        {
            return false;
        }

        return currentTrack.Id == playingTrack.Id;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
