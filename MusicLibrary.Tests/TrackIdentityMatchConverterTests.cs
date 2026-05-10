using MusicLibrary.Converters;
using MusicLibrary.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

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

    [Theory]
    [MemberData(nameof(InvalidBindingValues))]
    public void Convert_ReturnsFalse_ForInvalidBindingValues(object[] values)
    {
        var converter = new TrackIdentityMatchConverter();

        object result = converter.Convert(
            values,
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

    public static TheoryData<object[]> InvalidBindingValues()
    {
        var track = new Track { Id = 7, Title = "A", Artist = "B", Genre = "Рок", FilePath = "a.mp3" };

        return new TheoryData<object[]>
        {
            Array.Empty<object>(),
            new object[] { track },
            new object[] { DependencyProperty.UnsetValue, track },
            new object[] { track, DependencyProperty.UnsetValue },
            new object[] { Binding.DoNothing, track },
            new object[] { track, Binding.DoNothing },
            new object[] { "not a track", track },
            new object[] { track, "not a track" }
        };
    }
}
