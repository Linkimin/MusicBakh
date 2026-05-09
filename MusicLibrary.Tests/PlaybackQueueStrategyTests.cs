using MusicLibrary.Models;
using MusicLibrary.Services.Playback;

namespace MusicLibrary.Tests;

public sealed class PlaybackQueueStrategyTests
{
    private static readonly Track A = new() { Id = 1, Title = "A", Artist = "X", Genre = "Рок", FilePath = "a.mp3" };
    private static readonly Track B = new() { Id = 2, Title = "B", Artist = "X", Genre = "Рок", FilePath = "b.mp3" };
    private static readonly Track C = new() { Id = 3, Title = "C", Artist = "X", Genre = "Рок", FilePath = "c.mp3" };
    private static readonly Track Outside = new() { Id = 99, Title = "Z", Artist = "X", Genre = "Джаз", FilePath = "z.mp3" };

    private static readonly IReadOnlyList<Track> Three = new[] { A, B, C };
    private static readonly IReadOnlyList<Track> One = new[] { A };
    private static readonly IReadOnlyList<Track> Empty = Array.Empty<Track>();

    [Fact]
    public void NoRepeat_AtMiddle_ReturnsNextByIndex()
    {
        Assert.Equal(B, NoRepeatStrategy.Instance.GetNext(A, Three));
    }

    [Fact]
    public void NoRepeat_AtLast_ReturnsNull()
    {
        Assert.Null(NoRepeatStrategy.Instance.GetNext(C, Three));
    }

    [Fact]
    public void NoRepeat_EmptyList_ReturnsNull()
    {
        Assert.Null(NoRepeatStrategy.Instance.GetNext(A, Empty));
    }

    [Fact]
    public void NoRepeat_CurrentNotInList_ReturnsNull()
    {
        Assert.Null(NoRepeatStrategy.Instance.GetNext(Outside, Three));
    }

    [Fact]
    public void RepeatCurrent_AlwaysReturnsCurrent_EvenIfNotInList()
    {
        Assert.Equal(Outside, RepeatCurrentStrategy.Instance.GetNext(Outside, Three));
        Assert.Equal(B, RepeatCurrentStrategy.Instance.GetNext(B, Empty));
    }

    [Fact]
    public void RepeatLibrary_AtMiddle_ReturnsNextByIndex()
    {
        Assert.Equal(B, RepeatLibraryStrategy.Instance.GetNext(A, Three));
    }

    [Fact]
    public void RepeatLibrary_AtLast_WrapsToFirst()
    {
        Assert.Equal(A, RepeatLibraryStrategy.Instance.GetNext(C, Three));
    }

    [Fact]
    public void RepeatLibrary_SingleTrackList_ReturnsSameTrack()
    {
        Assert.Equal(A, RepeatLibraryStrategy.Instance.GetNext(A, One));
    }

    [Fact]
    public void RepeatLibrary_EmptyList_ReturnsNull()
    {
        Assert.Null(RepeatLibraryStrategy.Instance.GetNext(A, Empty));
    }

    [Fact]
    public void RepeatLibrary_CurrentNotInList_ReturnsFirst()
    {
        Assert.Equal(A, RepeatLibraryStrategy.Instance.GetNext(Outside, Three));
    }
}
