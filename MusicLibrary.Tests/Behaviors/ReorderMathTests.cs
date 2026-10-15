using System.Collections.Generic;
using MusicLibrary.Behaviors;
using Xunit;

namespace MusicLibrary.Tests.Behaviors;

public sealed class ReorderMathTests
{
    // 3 строки высотой 20, tops = 0,20,40.
    private static readonly IReadOnlyList<double> Tops = new[] { 0.0, 20.0, 40.0 };
    private static readonly IReadOnlyList<double> Heights = new[] { 20.0, 20.0, 20.0 };

    [Theory]
    [InlineData(2, 0)]    // над первой строкой (верхняя половина row0) → вставка перед 0
    [InlineData(12, 1)]   // нижняя половина row0 → вставка перед 1
    [InlineData(48, 2)]   // нижняя половина row2 → вставка в конец (индекс 2, т.к. clamp к Count-1 при reorder)
    public void TargetIndex_Maps_Y_To_Insertion_Position(double y, int expected)
    {
        Assert.Equal(expected, ReorderMath.TargetIndex(y, Tops, Heights));
    }

    // AdjustForRemoval: сырой индекс вставки (относительно ДО удаления source) → toPosition для Reorder,
    // который удаляет source ПЕРЕД вставкой. При движении вниз всё между source и точкой сдвигается на 1.
    [Theory]
    [InlineData(0, 2, 1)]   // A(0) роняем «между B и C» (raw=2) → ожидаем B,A,C → to=1
    [InlineData(0, 1, 0)]   // A(0) роняем на место B (raw=1) → to=0 (никуда не двинулся визуально)
    [InlineData(2, 0, 0)]   // C(2) роняем в начало (raw=0) → движение вверх, поправки нет → to=0
    [InlineData(1, 2, 1)]   // B(1) роняем «между B и C» (raw=2) → to=1 (стоит на месте)
    public void AdjustForRemoval_Compensates_Downward_Shift(int from, int raw, int expected)
    {
        Assert.Equal(expected, ReorderMath.AdjustForRemoval(raw, from));
    }

    [Fact]
    public void DataObject_RoundTrips_Track_Ids()
    {
        var data = DragDropListHelper.MakeData(new[] { 3, 7, 9 });
        Assert.True(DragDropListHelper.TryGetTrackIds(data, out var ids));
        Assert.Equal(new[] { 3, 7, 9 }, ids);
    }
}
