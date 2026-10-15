using MusicBakh.Core.Domain;

namespace MusicLibrary.Tests;

public sealed class DurationDisplayTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(59, "0:59")]
    [InlineData(3661, "61:01")]
    [InlineData(90000, "1500:00")]
    public void TrackDuration_DoesNotWrapAtOneHour(int seconds, string expected)
    {
        Assert.Equal(expected, new Track { Duration = TimeSpan.FromSeconds(seconds) }.DurationText);
    }
}
