using System.Collections;
using System.Linq;
using MusicBakh.Core.Domain;
using MusicLibrary.Behaviors;
using Xunit;

namespace MusicLibrary.Tests.Behaviors;

/// <summary>
/// Тесты чистого хелпера сборки батча id для DnD-мульти-выбора (Task 8). Мирроит стиль
/// ContextSelectionTests: тот же приём "клик внутри/вне мульти-выделения" применяется здесь
/// к началу drag — если перетаскиваемая строка входит в текущее выделение (>1 элемент),
/// в DataObject уходят id ВСЕХ выбранных треков, иначе — только перетаскиваемого.
/// </summary>
public sealed class DragDropListHelperSelectionTests
{
    private static Track MakeTrack(int id) => new() { Id = id, Title = "t" + id, FilePath = id + ".mp3" };

    [Fact]
    public void SelectedTrackIds_NullSelection_ReturnsOnlyDragged()
    {
        var dragged = MakeTrack(1);

        var result = DragDropListHelper.SelectedTrackIds(null, dragged);

        Assert.Equal(new[] { 1 }, result);
    }

    [Fact]
    public void SelectedTrackIds_DraggedOutsideSelection_ReturnsOnlyDragged()
    {
        var dragged = MakeTrack(1);
        var other1 = MakeTrack(2);
        var other2 = MakeTrack(3);
        IList selected = new ArrayList { other1, other2 };

        var result = DragDropListHelper.SelectedTrackIds(selected, dragged);

        Assert.Equal(new[] { 1 }, result);
    }

    [Fact]
    public void SelectedTrackIds_DraggedInsideMultiSelection_ReturnsAllSelectedIds()
    {
        var dragged = MakeTrack(1);
        var other = MakeTrack(2);
        IList selected = new ArrayList { dragged, other };

        var result = DragDropListHelper.SelectedTrackIds(selected, dragged);

        Assert.Equal(new[] { 1, 2 }, result.OrderBy(x => x));
    }

    [Fact]
    public void SelectedTrackIds_SingleItemSelection_ReturnsOnlyDragged()
    {
        // Выделена только сама перетаскиваемая строка (Count == 1) — не "мульти", ведём себя
        // как без выделения вообще (симметрично с ContextSelection.Resolve).
        var dragged = MakeTrack(1);
        IList selected = new ArrayList { dragged };

        var result = DragDropListHelper.SelectedTrackIds(selected, dragged);

        Assert.Equal(new[] { 1 }, result);
    }
}
