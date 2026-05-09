using MusicLibrary.Converters;
using MusicLibrary.Models;
using System.Globalization;

namespace MusicLibrary.Tests;

public sealed class TrackIdentityMatchConverterTests
{
    [Fact]
    public void Convert_ReturnsTrue_WhenTrackIdsMatch()
    {
        var converter = new TrackIdentityMatchConverter();
        var current = new Track { Id = 7, Title = "A", Artist = "B", Genre = "Рок", FilePath = "a.mp3" };
        var playing = new Track { Id = 7, Title = "Copy", Artist = "B", Genre = "Рок", FilePath = "copy.mp3" };

        object result = converter.Convert(
            new object[] { current, playing },
            typeof(bool),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.True((bool)result);
    }

    [Fact]
    public void Convert_ReturnsFalse_WhenTrackIdsDiffer()
    {
        var converter = new TrackIdentityMatchConverter();
        var current = new Track { Id = 7, Title = "A", Artist = "B", Genre = "Рок", FilePath = "a.mp3" };
        var playing = new Track { Id = 8, Title = "C", Artist = "D", Genre = "Джаз", FilePath = "c.mp3" };

        object result = converter.Convert(
            new object[] { current, playing },
            typeof(bool),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_ReturnsFalse_WhenAnyValueIsMissing()
    {
        var converter = new TrackIdentityMatchConverter();
        var current = new Track { Id = 7, Title = "A", Artist = "B", Genre = "Рок", FilePath = "a.mp3" };

        object result = converter.Convert(
            new object[] { current, null! },
            typeof(bool),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.False((bool)result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new TrackIdentityMatchConverter();

        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(
            value: true,
            new[] { typeof(Track), typeof(Track) },
            parameter: null!,
            CultureInfo.InvariantCulture));
    }
}
