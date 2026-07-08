using System.Windows;
using System.Windows.Input;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;

namespace MusicLibrary;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private System.Collections.Specialized.INotifyCollectionChanged? _tagFiltersSource;
    private System.Windows.Controls.Button? _morePillCached;

    public MainWindow(MainViewModel viewModel, IPlayerSettingsRepository playerSettingsRepository)
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;

        _viewModel = viewModel;

        // Гидратируем ViewModel сохранёнными настройками плеера (громкость, mute, режим повтора).
        var settings = playerSettingsRepository.Load();
        _viewModel.Volume = settings.Volume;
        _viewModel.IsMuted = settings.IsMuted;
        _viewModel.RepeatMode = settings.RepeatMode;

        // Активную вкладку сознательно НЕ восстанавливаем: приложение всегда стартует на
        // «Треки» (продуктовое решение — предсказуемый дефолт вместо «где был в прошлый раз»).
        // Персист active_view в KeyValueStore остаётся неиспользуемым — см. scope-deviations §1.0.4.

        DataContext = _viewModel;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowAppearance.Apply(this);
    }

    private void OnFindCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        // Ctrl+F: фокус и выделение текста в строке поиска. Это чисто UI-операция
        // над конкретным TextBox, поэтому живёт в code-behind, а не в команде VM.
        SearchBox.Focus();
        SearchBox.SelectAll();
        e.Handled = true;
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Эскейп — очистить и снять фокус. ViewModel получит пустой SearchText
            // через биндинг и вернётся к полной библиотеке.
            SearchBox.Clear();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Инлайн-создание плейлиста (PlaylistsRootTemplate, x:Name="PlaylistNameInput"):
        // KeyDown из TextBox внутри ResourceDictionary-шаблона не может быть подключён
        // атрибутом KeyDown="..." — ResourceDictionary без x:Class не резолвит обработчики
        // (MC6024). Событие — routed и всплывает до Window, поэтому ловим его здесь по
        // имени элемента-источника. Enter подтверждает имя, Esc отменяет создание.
        if (e.OriginalSource is System.Windows.Controls.TextBox { Name: "PlaylistNameInput" } nameInput)
        {
            if (e.Key == Key.Enter)
            {
                _viewModel.Playlists.ConfirmNewPlaylistName(nameInput.Text);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _viewModel.Playlists.IsCreating = false;
                e.Handled = true;
            }
            return;
        }

        // Инлайн-переименование плейлиста (PlaylistRowTemplate, x:Name="PlaylistRenameInput"):
        // DataContext строки — сам Playlist, поэтому берём его прямо с TextBox.DataContext
        // (тот же MC6024-обход, что и для создания выше). Enter подтверждает, Esc отменяет.
        if (e.OriginalSource is System.Windows.Controls.TextBox { Name: "PlaylistRenameInput" } renameInput)
        {
            if (renameInput.DataContext is Playlist targetPlaylist)
            {
                if (e.Key == Key.Enter)
                {
                    _viewModel.Playlists.RenamePlaylist(targetPlaylist, renameInput.Text);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    _viewModel.Playlists.CancelRenameCommand.Execute(null);
                    e.Handled = true;
                }
            }
            return;
        }

        // Esc на уровне окна — drill-down back, но только если фокус НЕ в строке
        // поиска: там Esc уже обрабатывается в OnSearchBoxKeyDown (очистка поиска)
        // и выставляет e.Handled=true, так что сюда событие не дойдёт.
        if (e.Key == Key.Escape && _viewModel.CanGoBack && !SearchBox.IsKeyboardFocusWithin)
        {
            _viewModel.BackCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        HookTagFiltersSync();
        RebuildTagChips();
        // Подписка на PropertyChanged VM: обновляем список в открытом popup тегов.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void HookTagFiltersSync()
    {
        if (_viewModel.TagFilters is System.Collections.Specialized.INotifyCollectionChanged ncc && _tagFiltersSource is null)
        {
            ncc.CollectionChanged += (_, _) => RebuildTagChips();
            _tagFiltersSource = ncc;
        }
    }

    private void RebuildTagChips()
    {
        System.Windows.Controls.Button morePill = EnsureMorePill();
        TagChipsPanel.Children.Clear();

        foreach (var item in _viewModel.TagFilters)
        {
            TagChipsPanel.Children.Add(CreateChip(item));
        }
        TagChipsPanel.Children.Add(morePill);
    }

    private System.Windows.Controls.Button EnsureMorePill()
    {
        if (_morePillCached is null)
        {
            var tb = new System.Windows.Controls.TextBlock();
            tb.Inlines.Add(new System.Windows.Documents.Run("+ "));
            var countRun = new System.Windows.Documents.Run();
            System.Windows.Data.BindingOperations.SetBinding(countRun,
                System.Windows.Documents.Run.TextProperty,
                new System.Windows.Data.Binding(nameof(MusicLibrary.Controls.OverflowChipPanel.HiddenCount))
                {
                    Source = TagChipsPanel,
                    Mode = System.Windows.Data.BindingMode.OneWay
                });
            tb.Inlines.Add(countRun);
            tb.Inlines.Add(new System.Windows.Documents.Run(" ещё ▾"));

            _morePillCached = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("MoreChipButtonStyle"),
                Content = tb
            };
            _morePillCached.Click += OnMoreTagsClick;
        }
        return _morePillCached;
    }

    private System.Windows.Controls.Border CreateChip(MusicLibrary.ViewModels.TagFilterItem item)
    {
        var chipBorder = new System.Windows.Controls.Border
        {
            Cursor = System.Windows.Input.Cursors.Hand,
            DataContext = item,
            Margin = new Thickness(0, 0, 6, 0)
        };

        // Стиль чипа с IsSelected-триггером для подсветки активного.
        var styleOverride = new Style(typeof(System.Windows.Controls.Border),
            (Style)FindResource("TagChipStyle"));
        var trigger = new System.Windows.DataTrigger
        {
            Binding = new System.Windows.Data.Binding(nameof(MusicLibrary.ViewModels.TagFilterItem.IsSelected)),
            Value = true
        };
        trigger.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xD4, 0xA5, 0x74))));
        trigger.Setters.Add(new Setter(System.Windows.Controls.Border.BorderBrushProperty,
            (System.Windows.Media.Brush)FindResource("PrimaryBrush")));
        styleOverride.Triggers.Add(trigger);
        chipBorder.Style = styleOverride;

        chipBorder.MouseLeftButtonUp += (_, _) => _viewModel.ToggleTagFilterCommand.Execute(item);

        var text = new System.Windows.Controls.TextBlock
        {
            Text = item.Tag.Name,
            Style = (Style)FindResource("TagChipTextStyle")
        };
        chipBorder.Child = text;
        return chipBorder;
    }

    private void OnMoreTagsClick(object sender, RoutedEventArgs e)
    {
        // Собираем список скрытых тегов: дети TagChipsPanel с Visibility=Collapsed
        // (кроме последнего — это сам More-пилюля). Порядок Children совпадает
        // с порядком VM.TagFilters, потому что RebuildTagChips сохраняет его.
        var hidden = new System.Collections.Generic.List<MusicLibrary.ViewModels.TagFilterItem>();
        int childCount = TagChipsPanel.Children.Count;
        for (int i = 0; i < childCount - 1; i++) // -1: пропускаем More-пилюлю
        {
            if (TagChipsPanel.Children[i].Visibility == Visibility.Collapsed
                && i < _viewModel.TagFilters.Count)
            {
                hidden.Add(_viewModel.TagFilters[i]);
            }
        }

        // DataContext явно выставляем на корневой Border popup-а, т.к. Popup живёт
        // в отдельном визуальном дереве и не наследует DataContext от MainWindow.
        // Корневой Border раздаёт DataContext дочерним StackPanel/ItemsControl/Button,
        // включая кнопку «Обновить список» (биндинг к RefreshTagFiltersCommand).
        if (MoreTagsPopup.Child is FrameworkElement popupRoot)
        {
            popupRoot.DataContext = _viewModel;
        }
        HiddenTagsList.ItemTemplate = (DataTemplate)FindResource("HiddenTagChipTemplate");
        HiddenTagsList.ItemsSource = hidden;
        MoreTagsPopup.IsOpen = true;
    }

    private void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        // DataContext popup-а — MainViewModel. Popup живёт в отдельном визуальном дереве
        // и не наследует DataContext от MainWindow автоматически.
        if (AddTagPopup.Child is FrameworkElement popupRoot)
        {
            popupRoot.DataContext = _viewModel;
        }
        AvailableTagsList.ItemsSource = _viewModel.AvailableTagsForAttach.ToList();
        AddTagPopup.IsOpen = !AddTagPopup.IsOpen;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Когда AvailableTagsForAttach меняется (например, после AttachTag из popup-а),
        // обновляем чипы в открытом popup-е, чтобы пользователь видел актуальный список.
        if (e.PropertyName == nameof(MusicLibrary.ViewModels.MainViewModel.AvailableTagsForAttach)
            && AddTagPopup?.IsOpen == true)
        {
            AvailableTagsList.ItemsSource = _viewModel.AvailableTagsForAttach.ToList();
        }
    }

    private void OnMoreFiltersClick(object sender, RoutedEventArgs e)
    {
        MoreFiltersPopup.IsOpen = !MoreFiltersPopup.IsOpen;
    }

    private void OnResetFiltersClick(object sender, RoutedEventArgs e)
    {
        // ClearFiltersCommand уже отработал через Binding.Command; здесь только закрываем popup.
        MoreFiltersPopup.IsOpen = false;
    }

    private void OnSeekDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _viewModel.IsSeeking = true;
    }

    private void OnSeekPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider)
        {
            _viewModel.SeekToCommand.Execute(TimeSpan.FromSeconds(slider.Value));
        }

        _viewModel.IsSeeking = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
