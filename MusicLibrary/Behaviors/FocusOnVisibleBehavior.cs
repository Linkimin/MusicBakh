using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MusicLibrary.Behaviors;

/// <summary>
/// Attached-поведение: переводит фокус на элемент в момент, когда он становится видимым
/// (например, инлайн-TextBox создания плейлиста, появляющийся по Playlists.IsCreating=true).
/// IsVisibleChanged — обычное CLR-событие (не routed), поэтому его нельзя перехватить
/// в ResourceDictionary без x:Class обработчиком в XAML; вместо этого подписываемся из кода.
/// </summary>
public static class FocusOnVisibleBehavior
{
    public static readonly DependencyProperty FocusOnVisibleProperty =
        DependencyProperty.RegisterAttached(
            "FocusOnVisible",
            typeof(bool),
            typeof(FocusOnVisibleBehavior),
            new PropertyMetadata(false, OnFocusOnVisibleChanged));

    public static bool GetFocusOnVisible(DependencyObject obj) => (bool)obj.GetValue(FocusOnVisibleProperty);

    public static void SetFocusOnVisible(DependencyObject obj, bool value) => obj.SetValue(FocusOnVisibleProperty, value);

    private static void OnFocusOnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        // Отписка на случай повторного применения свойства, чтобы не задваивать обработчик.
        element.IsVisibleChanged -= OnElementIsVisibleChanged;

        if ((bool)e.NewValue)
        {
            element.IsVisibleChanged += OnElementIsVisibleChanged;
        }
    }

    private static void OnElementIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue != true) return;
        if (sender is not FrameworkElement element) return;

        // Приоритет Input — фокус ставим уже после того, как layout применит новую видимость.
        element.Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() =>
        {
            element.Focus();
            if (element is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }));
    }
}
