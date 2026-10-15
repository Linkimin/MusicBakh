using System.Collections.Generic;

namespace MusicLibrary.Behaviors;

/// <summary>Чистая геометрия reorder: по Y-координате дропа возвращает индекс вставки в список строк.</summary>
public static class ReorderMath
{
    /// <summary>
    /// rowTops/rowHeights — верхние границы и высоты строк (в координатах контейнера). Возвращает
    /// индекс строки, ПЕРЕД которой ставится перетаскиваемый элемент (верхняя половина строки → перед ней,
    /// нижняя половина → перед следующей). Клампится в [0, Count-1] для случая reorder внутри одного списка.
    /// </summary>
    public static int TargetIndex(double dropY, IReadOnlyList<double> rowTops, IReadOnlyList<double> rowHeights)
    {
        int count = rowTops.Count;
        if (count == 0) return 0;
        for (int i = 0; i < count; i++)
        {
            double mid = rowTops[i] + rowHeights[i] / 2.0;
            if (dropY < mid) return i;
        }
        return count - 1;
    }

    /// <summary>
    /// Преобразует «сырой» индекс вставки (посчитанный <see cref="TargetIndex"/> относительно списка
    /// ДО удаления перетаскиваемого элемента) в <c>toPosition</c> для <c>IPlaylistRepository.Reorder</c>,
    /// который сначала удаляет элемент из <paramref name="fromPosition"/>, а потом вставляет. При движении
    /// вниз (<paramref name="rawTarget"/> &gt; <paramref name="fromPosition"/>) удаление source сдвигает
    /// все последующие на одну позицию — поэтому вычитаем 1. Движение вверх коррекции не требует.
    /// </summary>
    public static int AdjustForRemoval(int rawTarget, int fromPosition)
        => rawTarget > fromPosition ? rawTarget - 1 : rawTarget;
}
