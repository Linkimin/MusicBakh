using System;
using System.Windows;
using System.Windows.Input;

namespace MusicLibrary.Behaviors;

/// <summary>
/// Базовая DnD-механика для списков: инициация drag по порогу движения и приём drop.
/// Поведение навешивается в XAML (Task 8); здесь — общая инфраструктура, чтобы add и reorder
/// не дублировали код. Формат DataObject: строковый формат "musicbakh-tracks" со списком int Id.
/// </summary>
public static class DragDropListHelper
{
    public const string TrackIdsFormat = "musicbakh-track-ids";

    private static Point _start;
    private static bool _armed;

    /// <summary>Вызвать из PreviewMouseLeftButtonDown строки-источника: запоминает точку.</summary>
    public static void ArmDrag(MouseButtonEventArgs e, IInputElement reference)
    {
        _start = e.GetPosition(reference);
        _armed = true;
    }

    /// <summary>Вызвать из PreviewMouseMove: если превышен порог — вернуть true (пора начинать DoDragDrop).</summary>
    public static bool ShouldBeginDrag(MouseEventArgs e, IInputElement reference)
    {
        if (!_armed || e.LeftButton != MouseButtonState.Pressed) return false;
        var p = e.GetPosition(reference);
        return Math.Abs(p.X - _start.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(p.Y - _start.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    public static void Disarm() => _armed = false;

    /// <summary>Собирает DataObject из id треков.</summary>
    public static DataObject MakeData(int[] trackIds)
    {
        var data = new DataObject();
        data.SetData(TrackIdsFormat, trackIds);
        return data;
    }

    public static bool TryGetTrackIds(IDataObject data, out int[] ids)
    {
        if (data.GetDataPresent(TrackIdsFormat) && data.GetData(TrackIdsFormat) is int[] arr)
        { ids = arr; return true; }
        ids = Array.Empty<int>();
        return false;
    }
}
