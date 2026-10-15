using System.Collections;
using System.Windows;
using System.Windows.Controls;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;

namespace MusicLibrary.Behaviors;

/// <summary>
/// Attached-поведение: фиксирует «на каком треке/выделении открыто контекстное меню».
/// Вешается на корневой элемент строки трека (Border/Grid). При открытии меню
/// (ContextMenuOpening) читает MainViewModel из fe.Tag (строка несёт
/// Tag="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Window}}" —
/// тот же приём, что нужен ContextMenu для PlacementTarget.Tag, см. XAML), Track — из
/// fe.DataContext, поднимается по дереву до ItemsControl/Selector за SelectedItems (может
/// не найтись для не-Selector-контролов — это нормально, тогда работаем как с одиночным кликом),
/// и передаёт результат чистой функции ContextSelection.Resolve в PlaylistsViewModel.SetContextTarget.
///
/// Заодно сохраняет позицию строки (ItemsControl.AlternationIndex её ListBoxItem-контейнера)
/// в собственное attached-свойство ResolvedPosition НА САМОЙ СТРОКЕ (не на ListBoxItem):
/// AlternationIndex не унаследуемое (Inherits=false) свойство, поэтому
/// "PlacementTarget.(ItemsControl.AlternationIndex)" в XAML контекст-меню читал бы его
/// прямо с PlacementTarget (Grid строки), где оно никогда не выставлялось (только на
/// ListBoxItem-контейнере) — CommandParameter «Убрать из плейлиста» всегда был бы 0.
/// ResolvedPosition — обходной путь: заполняется здесь при каждом открытии меню, а XAML
/// читает его с PlacementTarget напрямую (никакого AncestorType-поиска в binding не нужно).
///
/// Само событийное связывание не юнит-тестируется (визуальный smoke, Task 10);
/// вся ветвящаяся логика вынесена в ContextSelection (покрыта ContextSelectionTests).
/// </summary>
public static class TrackContextMenuBehavior
{
    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached(
            "Attach",
            typeof(bool),
            typeof(TrackContextMenuBehavior),
            new PropertyMetadata(false, OnAttachChanged));

    public static readonly DependencyProperty ResolvedPositionProperty =
        DependencyProperty.RegisterAttached(
            "ResolvedPosition",
            typeof(int),
            typeof(TrackContextMenuBehavior),
            new PropertyMetadata(0));

    public static bool GetAttach(DependencyObject obj) => (bool)obj.GetValue(AttachProperty);

    public static void SetAttach(DependencyObject obj, bool value) => obj.SetValue(AttachProperty, value);

    public static int GetResolvedPosition(DependencyObject obj) => (int)obj.GetValue(ResolvedPositionProperty);

    public static void SetResolvedPosition(DependencyObject obj, int value) => obj.SetValue(ResolvedPositionProperty, value);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        // Отписка на случай повторного применения свойства, чтобы не задваивать обработчик.
        element.ContextMenuOpening -= OnContextMenuOpening;

        if ((bool)e.NewValue)
        {
            element.ContextMenuOpening += OnContextMenuOpening;
        }
    }

    private static void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;

        SetResolvedPosition(fe, FindAlternationIndex(fe));

        if (fe.Tag is not MainViewModel vm) return;
        if (fe.DataContext is not Track clicked) return;

        IList? selectedItems = FindSelectedItems(fe);
        vm.Playlists.SetContextTarget(ContextSelection.Resolve(selectedItems, clicked));
    }

    /// <summary>
    /// Поднимается от строки вверх до ближайшего ListBoxItem-контейнера и читает его
    /// ItemsControl.AlternationIndex (0, если контейнер не найден — например, строка не
    /// внутри ListBox с AlternationCount, а в обычном ItemsControl).
    /// </summary>
    private static int FindAlternationIndex(DependencyObject element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is ListBoxItem item)
            {
                return ItemsControl.GetAlternationIndex(item);
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current)
                ?? LogicalTreeHelper.GetParent(current);
        }
        return 0;
    }

    /// <summary>
    /// Поднимается от строки вверх по визуальному/логическому дереву до ближайшего Selector
    /// (ListBox и т.п.), у которого есть SelectedItems. Для ItemsControl без выделения
    /// (обычный ItemsControl) вернёт null — вызывающий код это уже учитывает.
    /// </summary>
    private static IList? FindSelectedItems(DependencyObject element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is ListBox listBox)
            {
                return listBox.SelectedItems;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current)
                ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
