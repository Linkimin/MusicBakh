using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;

namespace MusicLibrary.Behaviors;

/// <summary>
/// Базовая DnD-механика для списков: инициация drag по порогу движения и приём drop.
/// Здесь — общая инфраструктура (Task 5, spike) + attached properties, навешиваемые в XAML
/// (Task 8): <see cref="IsDragSourceProperty"/> на строке-источнике трека,
/// <see cref="PlaylistDropTargetProperty"/> на строке плейлиста (add-only),
/// <see cref="ReorderTargetProperty"/> на треклисте детали плейлиста (add + reorder).
/// Формат DataObject: строковый формат "musicbakh-track-ids" со списком int Id
/// (см. <see cref="TrackIdsFormat"/> — раньше в этом комментарии было "musicbakh-tracks",
/// исправлено на фактическое имя константы).
/// </summary>
public static class DragDropListHelper
{
    public const string TrackIdsFormat = "musicbakh-track-ids";

    /// <summary>
    /// Формат для позиции перетаскиваемой строки внутри её ИСХОДНОГО списка — присутствует
    /// в DataObject ТОЛЬКО когда drag начат reorder-строкой (IsReorderSource), см. MakeReorderData.
    /// Позиция едет вместе с самим DataObject (а не как побочное состояние на ListBox), поэтому
    /// не может "протухнуть", если drag завершился вне валидной цели (drop отменён, ушёл за
    /// пределы окна и т.п.) — в отличие от отдельного attached-свойства на ListBox, которое
    /// пришлось бы аккуратно сбрасывать на каждом таком пути отдельно.
    /// </summary>
    private const string SourcePositionFormat = "musicbakh-reorder-source-position";

    /// <summary>
    /// Состояние "вооружения" drag per-исходный-элемент. Раньше _start/_armed были статическими
    /// полями класса — общими на ВСЕ списки процесса разом. Это давало утечку состояния между
    /// списками: PreviewMouseLeftButtonDown на строке трек-листа "Треки" мог быть перекрыт (без
    /// парного Disarm) движением мыши уже над строкой плейлиста, и ShouldBeginDrag ошибочно
    /// решал бы, что drag пора начинать, — используя _start от СОВСЕМ ДРУГОГО элемента.
    /// ConditionalWeakTable даёт per-источник состояние без явного attached DependencyProperty
    /// (не нужно объявлять ещё один DP только для private struct) и без утечки памяти: запись
    /// живёт, пока жив сам FrameworkElement-источник, GC чистит её вместе с ним.
    /// </summary>
    private static readonly ConditionalWeakTable<IInputElement, DragArmState> _armStates = new();

    private sealed class DragArmState
    {
        public Point Start;
        public bool Armed;
    }

    private static DragArmState GetOrCreateState(IInputElement reference)
        => _armStates.GetOrCreateValue(reference);

    /// <summary>Вызвать из PreviewMouseLeftButtonDown строки-источника: запоминает точку.</summary>
    public static void ArmDrag(MouseButtonEventArgs e, IInputElement reference)
    {
        var state = GetOrCreateState(reference);
        state.Start = e.GetPosition(reference);
        state.Armed = true;
    }

    /// <summary>Вызвать из PreviewMouseMove: если превышен порог — вернуть true (пора начинать DoDragDrop).</summary>
    public static bool ShouldBeginDrag(MouseEventArgs e, IInputElement reference)
    {
        if (!_armStates.TryGetValue(reference, out var state) || !state.Armed) return false;
        if (e.LeftButton != MouseButtonState.Pressed) return false;
        var p = e.GetPosition(reference);
        return Math.Abs(p.X - state.Start.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(p.Y - state.Start.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    /// <summary>Снимает "вооружение" конкретного источника. Вызывается на всех выходах: после
    /// успешного DoDragDrop, после MouseUp без drag, и (страховочно) при IsDragSource=false.</summary>
    public static void Disarm(IInputElement reference)
    {
        if (_armStates.TryGetValue(reference, out var state)) state.Armed = false;
    }

    /// <summary>Собирает DataObject из id треков (add-drag: строка вне плейлиста).</summary>
    public static DataObject MakeData(int[] trackIds)
    {
        var data = new DataObject();
        data.SetData(TrackIdsFormat, trackIds);
        return data;
    }

    /// <summary>Собирает DataObject для reorder-drag: id + позиция строки в исходном списке.
    /// Всегда ровно один трек (схваченная строка) — group-reorder не поддержан, см.
    /// OnReorderSourcePreviewMouseMove: мульти-выбор для reorder-источника игнорируется
    /// намеренно, чтобы same-list drag никогда не уходил в add-batch фолбэк (review-фикс,
    /// раньше это приводило к дублированию треков в плейлисте).</summary>
    private static DataObject MakeReorderData(int trackId, int sourcePosition)
    {
        var data = new DataObject();
        data.SetData(TrackIdsFormat, new[] { trackId });
        data.SetData(SourcePositionFormat, sourcePosition);
        return data;
    }

    public static bool TryGetTrackIds(IDataObject data, out int[] ids)
    {
        if (data.GetDataPresent(TrackIdsFormat) && data.GetData(TrackIdsFormat) is int[] arr)
        { ids = arr; return true; }
        ids = Array.Empty<int>();
        return false;
    }

    private static bool TryGetSourcePosition(IDataObject data, out int position)
    {
        if (data.GetDataPresent(SourcePositionFormat) && data.GetData(SourcePositionFormat) is int p)
        { position = p; return true; }
        position = -1;
        return false;
    }

    /// <summary>
    /// Собирает батч id для начала drag с мульти-выбором: если перетаскиваемый трек входит
    /// в текущее выделение (>1 элемент выделено) — берём id ВСЕХ выбранных треков, иначе —
    /// только перетаскиваемого. Симметрично с ContextSelection.Resolve (контекст-меню, Task 7),
    /// только на выходе int[] id вместо IReadOnlyList&lt;Track&gt;, потому что DataObject
    /// в DoDragDrop несёт именно id (см. MakeData/TryGetTrackIds).
    /// </summary>
    public static int[] SelectedTrackIds(IList? selectedItems, Track dragged)
    {
        if (selectedItems is null) return new[] { dragged.Id };
        var tracks = selectedItems.OfType<Track>().ToList();
        return tracks.Count > 1 && tracks.Contains(dragged)
            ? tracks.Select(t => t.Id).ToArray()
            : new[] { dragged.Id };
    }

    // ========================= IsDragSource =========================

    /// <summary>
    /// Навешивается на корневой элемент строки трека (Border/Grid). Подписывает
    /// PreviewMouseLeftButtonDown (ArmDrag) / PreviewMouseMove (ShouldBeginDrag → DoDragDrop)
    /// / PreviewMouseLeftButtonUp (Disarm — на случай клика без движения, чтобы не оставлять
    /// "вооружённым" элемент до следующего MouseDown). Список, для сбора мульти-выделения,
    /// ищется подъёмом по дереву до ближайшего Selector (ListBox) — тот же приём, что
    /// TrackContextMenuBehavior.FindSelectedItems.
    /// </summary>
    public static readonly DependencyProperty IsDragSourceProperty =
        DependencyProperty.RegisterAttached(
            "IsDragSource",
            typeof(bool),
            typeof(DragDropListHelper),
            new PropertyMetadata(false, OnIsDragSourceChanged));

    public static bool GetIsDragSource(DependencyObject obj) => (bool)obj.GetValue(IsDragSourceProperty);

    public static void SetIsDragSource(DependencyObject obj, bool value) => obj.SetValue(IsDragSourceProperty, value);

    private static void OnIsDragSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        element.PreviewMouseLeftButtonDown -= OnSourcePreviewMouseLeftButtonDown;
        element.PreviewMouseMove -= OnSourcePreviewMouseMove;
        element.PreviewMouseLeftButtonUp -= OnSourcePreviewMouseLeftButtonUp;

        if ((bool)e.NewValue)
        {
            element.PreviewMouseLeftButtonDown += OnSourcePreviewMouseLeftButtonDown;
            element.PreviewMouseMove += OnSourcePreviewMouseMove;
            element.PreviewMouseLeftButtonUp += OnSourcePreviewMouseLeftButtonUp;
        }
        else
        {
            Disarm(element);
        }
    }

    private static void OnSourcePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        ArmDrag(e, fe);
    }

    private static void OnSourcePreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Клик без drag (простой выбор строки) — снимаем "вооружение", чтобы следующее
        // движение мыши (уже без кнопки) не подхватило устаревшую точку старта.
        if (sender is not FrameworkElement fe) return;
        Disarm(fe);
    }

    private static void OnSourcePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (!ShouldBeginDrag(e, fe)) return;
        if (fe.DataContext is not Track dragged) return;

        // Снимаем "вооружение" ДО DoDragDrop: это модальный вызов (крутит свой message loop),
        // и повторный вход сюда (например, вложенный MouseMove) не должен начать второй drag.
        Disarm(fe);

        IList? selected = FindSelectedItems(fe);
        int[] ids = SelectedTrackIds(selected, dragged);

        DragDrop.DoDragDrop(fe, MakeData(ids), DragDropEffects.Copy | DragDropEffects.Move);
    }

    /// <summary>Поднимается от строки вверх по визуальному/логическому дереву до ближайшего
    /// Selector (ListBox), чтобы прочитать его SelectedItems. Копия
    /// TrackContextMenuBehavior.FindSelectedItems — вынести в общий helper не стали, т.к. это
    /// увеличило бы связность двух behaviors ради 8 строк кода; если появится третий потребитель —
    /// стоит вынести.</summary>
    private static IList? FindSelectedItems(DependencyObject element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is ListBox listBox) return listBox.SelectedItems;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current)
                ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    // ========================= PlaylistDropTarget (add-only, строка плейлиста) =========================

    /// <summary>
    /// Навешивается на строку плейлиста в корневом списке (PlaylistRowTemplate). AllowDrop +
    /// DragOver-хайлайт (меняет Background, пока над строкой висит валидный drag) + Drop →
    /// DropTracksOnPlaylistCommand с параметром (Playlist target, IReadOnlyList&lt;int&gt; ids).
    /// Playlist читается из DataContext строки (сам элемент Playlists.Playlists), MainViewModel —
    /// из fe.Tag (тот же приём, что Tag="{Binding DataContext, RelativeSource={RelativeSource
    /// AncestorType=Window}}", уже используемый для ContextMenu.PlacementTarget.Tag в Task 7).
    /// </summary>
    public static readonly DependencyProperty PlaylistDropTargetProperty =
        DependencyProperty.RegisterAttached(
            "PlaylistDropTarget",
            typeof(bool),
            typeof(DragDropListHelper),
            new PropertyMetadata(false, OnPlaylistDropTargetChanged));

    public static bool GetPlaylistDropTarget(DependencyObject obj) => (bool)obj.GetValue(PlaylistDropTargetProperty);

    public static void SetPlaylistDropTarget(DependencyObject obj, bool value) => obj.SetValue(PlaylistDropTargetProperty, value);

    private static void OnPlaylistDropTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        element.DragOver -= OnPlaylistRowDragOver;
        element.DragLeave -= OnPlaylistRowDragLeave;
        element.Drop -= OnPlaylistRowDrop;

        if ((bool)e.NewValue)
        {
            element.AllowDrop = true;
            element.DragOver += OnPlaylistRowDragOver;
            element.DragLeave += OnPlaylistRowDragLeave;
            element.Drop += OnPlaylistRowDrop;
        }
    }

    private static void OnPlaylistRowDragOver(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        bool valid = TryGetTrackIds(e.Data, out _);
        e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None;
        SetIsDropHighlighted(fe, valid);
        e.Handled = true;
    }

    private static void OnPlaylistRowDragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe) SetIsDropHighlighted(fe, false);
    }

    private static void OnPlaylistRowDrop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        SetIsDropHighlighted(fe, false);

        if (!TryGetTrackIds(e.Data, out int[] ids)) return;
        if (fe.DataContext is not Playlist playlist) return;
        if (fe.Tag is not MainViewModel vm) return;

        vm.DropTracksOnPlaylistCommand.Execute((playlist, (IReadOnlyList<int>)ids));
        e.Handled = true;
    }

    // ========================= ReorderTarget (add + reorder, треклист детали плейлиста) =========================

    /// <summary>
    /// Навешивается на ListBox с треками внутри PlaylistDetail. Обрабатывает ДВА сценария Drop:
    /// (1) добавление новых треков извне (drag из вкладки «Треки»/альбома/исполнителя — трек-id,
    /// которых ещё нет в этом плейлисте по позиции строки под курсором, роли не играет: add всегда
    /// идёт через DropTracksOnPlaylistCommand, дубликаты — осознанно разрешены, как и через
    /// контекст-меню «Добавить в плейлист»); (2) reorder — drag начат ВНУТРИ этого же ListBox
    /// (тогда используем позицию перетаскиваемой строки как fromPosition и считаем to через
    /// ReorderMath). Различаем сценарии по тому, содержит ли e.Data позицию источника
    /// (SourcePositionFormat) — она проставляется только при старте drag из ЭТОГО контрола
    /// (см. OnReorderSourcePreviewMouseMove ниже, переопределяющей обычный IsDragSource на
    /// строке ради доступа к позиции строки). Same-list drag (SourcePositionFormat присутствует
    /// и IsSameList подтверждает) ВСЕГДА обрабатывается как reorder ОДНОЙ схваченной строки и
    /// никогда не проваливается в add-batch — независимо от того, сколько строк было выделено
    /// в момент начала drag (review-фикс: раньше multi-select same-list drag дублировал треки).
    /// </summary>
    public static readonly DependencyProperty ReorderTargetProperty =
        DependencyProperty.RegisterAttached(
            "ReorderTarget",
            typeof(bool),
            typeof(DragDropListHelper),
            new PropertyMetadata(false, OnReorderTargetChanged));

    public static bool GetReorderTarget(DependencyObject obj) => (bool)obj.GetValue(ReorderTargetProperty);

    public static void SetReorderTarget(DependencyObject obj, bool value) => obj.SetValue(ReorderTargetProperty, value);

    private static void OnReorderTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        listBox.DragOver -= OnReorderDragOver;
        listBox.DragLeave -= OnReorderDragLeave;
        listBox.Drop -= OnReorderDrop;

        if ((bool)e.NewValue)
        {
            listBox.AllowDrop = true;
            listBox.DragOver += OnReorderDragOver;
            listBox.DragLeave += OnReorderDragLeave;
            listBox.Drop += OnReorderDrop;
        }
    }

    private static void OnReorderDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        bool valid = TryGetTrackIds(e.Data, out _);
        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        SetIsDropHighlighted(listBox, valid);
        e.Handled = true;
    }

    private static void OnReorderDragLeave(object sender, DragEventArgs e)
    {
        if (sender is ListBox listBox) SetIsDropHighlighted(listBox, false);
    }

    private static void OnReorderDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        SetIsDropHighlighted(listBox, false);

        if (!TryGetTrackIds(e.Data, out int[] ids)) return;
        if (listBox.Tag is not MainViewModel vm) return;

        // Reorder-путь: e.Data несёт SourcePositionFormat ТОЛЬКО когда drag начат
        // reorder-строкой (см. MakeReorderData/OnReorderSourcePreviewMouseMove) — позиция едет
        // вместе с самим DataObject, поэтому не зависит от того, из какого ListBox её читаем
        // (в отличие от побочного состояния на элементе). Если её нет — это add-drop извне
        // (drag из вкладки «Треки»/альбома/исполнителя): делегируем DropTracksOnPlaylistCommand,
        // тот же путь, что и строка плейлиста в корневом списке.
        //
        // ids.Length == 1 здесь всегда true, когда SourcePositionFormat присутствует — это
        // инвариант MakeReorderData (см. её doc), а не отдельная ветвь логики. Оставлен как
        // defensive-проверка. КРИТИЧНО: same-list-производный drag (позиция резолвится,
        // IsSameList подтверждает) обязан попасть именно в этот if и вернуться через
        // ReorderPlaylistCommand — ветка else ниже (add-batch) должна обслуживать ТОЛЬКО
        // кросс-списочные drops (иначе — дублирование треков, см. review-фикс).
        if (TryGetSourcePosition(e.Data, out int from) && ids.Length == 1 && IsSameList(listBox, from, ids[0]))
        {
            var geometry = MeasureRows(listBox);
            double dropY = e.GetPosition(listBox).Y;
            int raw = ReorderMath.TargetIndex(dropY, geometry.Tops, geometry.Heights);
            int to = ReorderMath.AdjustForRemoval(raw, from);
            vm.ReorderPlaylistCommand.Execute((from, to));
        }
        else if (vm.CurrentLeftColumn is MusicLibrary.Services.Library.LeftColumnState.PlaylistDetail detail)
        {
            vm.DropTracksOnPlaylistCommand.Execute((detail.Playlist.Playlist, (IReadOnlyList<int>)ids));
        }

        e.Handled = true;
    }

    /// <summary>Сверяет, что id, зашитый в SourcePositionFormat при старте drag, действительно
    /// совпадает с треком на fromPosition в ТЕКУЩЕМ списке — защита от рассинхрона, если список
    /// успел измениться между началом drag и Drop (маловероятно в рамках одного модального
    /// DoDragDrop, но дёшево проверить), а также от drop reorder-данных, начатых в ДРУГОМ
    /// экземпляре ReorderTarget-списка (не должно происходить — в приложении открыта одна
    /// деталь плейлиста за раз, — но проверка дешёвая и убирает саму возможность вопроса).</summary>
    private static bool IsSameList(ListBox listBox, int fromPosition, int draggedId)
    {
        if (fromPosition < 0 || fromPosition >= listBox.Items.Count) return false;
        return listBox.Items[fromPosition] is Track t && t.Id == draggedId;
    }

    /// <summary>Строит массивы верхних границ/высот строк-контейнеров ListBox в его собственных
    /// координатах — сырьё для ReorderMath.TargetIndex.</summary>
    private static (IReadOnlyList<double> Tops, IReadOnlyList<double> Heights) MeasureRows(ListBox listBox)
    {
        var tops = new List<double>();
        var heights = new List<double>();
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
            {
                // Контейнер ещё не сгенерирован (виртуализация) — не должно случаться для
                // playlist-треклистов (короткие списки, виртуализация практически не режет их),
                // но на случай подстраховываемся нулевой высотой, чтобы не уронить весь Drop.
                tops.Add(0);
                heights.Add(0);
                continue;
            }
            var topLeft = container.TransformToAncestor(listBox).Transform(new Point(0, 0));
            tops.Add(topLeft.Y);
            heights.Add(container.ActualHeight);
        }
        return (tops, heights);
    }

    // ========================= Reorder-source: позиция перетаскиваемой строки =========================

    /// <summary>
    /// Аналог IsDragSource, но также зашивает исходную позицию строки в DataObject (см.
    /// MakeReorderData/SourcePositionFormat) — позиция едет вместе с самим перетаскиванием,
    /// а не хранится побочным состоянием где-либо. Навешивается ВМЕСТО IsDragSource на строки
    /// треклиста внутри PlaylistDetail (add-source строки типа TrackCardTemplate вне плейлиста
    /// продолжают использовать простой IsDragSource, т.к. позиция им не нужна — они не
    /// участвуют в reorder).
    /// </summary>
    public static readonly DependencyProperty IsReorderSourceProperty =
        DependencyProperty.RegisterAttached(
            "IsReorderSource",
            typeof(bool),
            typeof(DragDropListHelper),
            new PropertyMetadata(false, OnIsReorderSourceChanged));

    public static bool GetIsReorderSource(DependencyObject obj) => (bool)obj.GetValue(IsReorderSourceProperty);

    public static void SetIsReorderSource(DependencyObject obj, bool value) => obj.SetValue(IsReorderSourceProperty, value);

    private static void OnIsReorderSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        element.PreviewMouseLeftButtonDown -= OnSourcePreviewMouseLeftButtonDown;
        element.PreviewMouseMove -= OnReorderSourcePreviewMouseMove;
        element.PreviewMouseLeftButtonUp -= OnSourcePreviewMouseLeftButtonUp;

        if ((bool)e.NewValue)
        {
            element.PreviewMouseLeftButtonDown += OnSourcePreviewMouseLeftButtonDown;
            element.PreviewMouseMove += OnReorderSourcePreviewMouseMove;
            element.PreviewMouseLeftButtonUp += OnSourcePreviewMouseLeftButtonUp;
        }
        else
        {
            Disarm(element);
        }
    }

    private static void OnReorderSourcePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (!ShouldBeginDrag(e, fe)) return;
        if (fe.DataContext is not Track dragged) return;

        Disarm(fe);

        // Позиция строки — AlternationIndex её ListBoxItem-контейнера (тот же приём, что
        // TrackContextMenuBehavior.FindAlternationIndex): PlaylistDetailTemplate уже включает
        // AlternationCount="{Binding Playlist.Tracks.Count}" для контекст-меню (Task 7), поэтому
        // переиспользуем его же для reorder-позиции — не нужно вводить второй счётчик позиции.
        //
        // Мульти-выбор здесь ВСЕГДА игнорируется для reorder-источника: раньше (до review-фикса)
        // при >1 выбранном треке отправлялся обычный add-batch DataObject (без позиции), и
        // OnReorderDrop, не найдя SourcePositionFormat, делегировал его в
        // DropTracksOnPlaylistCommand — это ДУБЛИРОВАЛО уже присутствующие в плейлисте треки
        // (add всегда просто добавляет строки, не переставляет существующие). Настоящий
        // multi-row group-reorder не реализован (см. план) — предсказуемое поведение вместо
        // этого: перетаскивается ТОЛЬКО схваченная строка (dragged), остальное выделение для
        // reorder-жеста не участвует. Ручной smoke (Task 10) проверяет именно этот сценарий.
        int position = FindAlternationIndex(fe);

        DataObject data = MakeReorderData(dragged.Id, position);

        DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
    }

    /// <summary>Поднимается от строки вверх до ближайшего ListBoxItem-контейнера и читает его
    /// ItemsControl.AlternationIndex (0, если контейнер не найден). Копия
    /// TrackContextMenuBehavior.FindAlternationIndex — см. пояснение у FindSelectedItems о том,
    /// почему не вынесено в общий helper.</summary>
    private static int FindAlternationIndex(DependencyObject element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is ListBoxItem item) return ItemsControl.GetAlternationIndex(item);
            current = System.Windows.Media.VisualTreeHelper.GetParent(current)
                ?? LogicalTreeHelper.GetParent(current);
        }
        return 0;
    }

    // ========================= Визуальный DragOver-хайлайт =========================

    /// <summary>Read-only с точки зрения XAML: используется только Style.Trigger'ом в шаблоне
    /// (простая подсветка фона во время валидного DragOver). Полноценный insertion-line adorner
    /// намеренно не делается — план (docs/superpowers/plans) относит визуальную полировку к
    /// ручному smoke (Task 10), здесь достаточно минимального фидбека.</summary>
    public static readonly DependencyProperty IsDropHighlightedProperty =
        DependencyProperty.RegisterAttached(
            "IsDropHighlighted",
            typeof(bool),
            typeof(DragDropListHelper),
            new PropertyMetadata(false));

    public static bool GetIsDropHighlighted(DependencyObject obj) => (bool)obj.GetValue(IsDropHighlightedProperty);

    public static void SetIsDropHighlighted(DependencyObject obj, bool value) => obj.SetValue(IsDropHighlightedProperty, value);
}
