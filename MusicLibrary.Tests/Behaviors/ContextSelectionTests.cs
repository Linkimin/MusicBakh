using System.Collections;
using System.Collections.Generic;
using MusicBakh.Core.Domain;
using MusicLibrary.Behaviors;
using Xunit;

namespace MusicLibrary.Tests.Behaviors;

public sealed class ContextSelectionTests
{
    private static Track MakeTrack(int id) => new() { Id = id, Title = "t" + id, FilePath = id + ".mp3" };

    [Fact]
    public void Resolve_NullSelection_ReturnsOnlyClicked()
    {
        var clicked = MakeTrack(1);

        var result = ContextSelection.Resolve(null, clicked);

        Assert.Equal(new[] { clicked }, result);
    }

    [Fact]
    public void Resolve_ClickedOutsideSelection_ReturnsOnlyClicked()
    {
        var clicked = MakeTrack(1);
        var other1 = MakeTrack(2);
        var other2 = MakeTrack(3);
        IList selectedItems = new ArrayList { other1, other2 };

        var result = ContextSelection.Resolve(selectedItems, clicked);

        Assert.Equal(new[] { clicked }, result);
    }

    [Fact]
    public void Resolve_ClickedInsideMultiSelection_ReturnsAllSelected()
    {
        var clicked = MakeTrack(1);
        var other = MakeTrack(2);
        IList selectedItems = new ArrayList { clicked, other };

        var result = ContextSelection.Resolve(selectedItems, clicked);

        Assert.Equal(new List<Track> { clicked, other }, result);
    }
}
