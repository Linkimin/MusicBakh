using MusicBakh.Application.Abstractions;
using MusicBakh.Application.Contracts;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.Commands;
using MusicLibrary.Services.Library;
using MusicLibrary.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace MusicLibrary.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private const string AllGenres = "Все жанры";

    // Лимит относится только к виджету «недавнее» в правой колонке MainWindow:
    // показываем последние 50 событий (с дублями), потому что виджет — это
    // «что играло прямо сейчас», а не журнал. Полная история без лимита доступна
    // через IListeningHistoryRepository.GetAll и используется в StatsWindow.
    private const int MaxHistoryItems = 50;

    private readonly List<Track> _allTracks;
    private readonly ITrackRepository _trackRepository;
    private readonly IFileService _fileService;
    private readonly IMusicStoragePaths? _storagePaths;
    private readonly ISaveFileDialogService _saveFileDialogService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IAddTrackDialogService? _addTrackDialogService;
    private readonly IConfirmationService? _confirmationService;
    private readonly IListeningHistoryRepository _listeningHistoryRepository;
    private readonly IPlayerSettingsRepository _playerSettingsRepository;
    private readonly ISearchService? _searchService;
    private readonly IStatsWindowService? _statsWindowService;
    private readonly ITagsWindowService? _tagsWindowService;
    private readonly ITagRepository? _tagRepository;
    private readonly PlaylistsViewModel? _playlists;
    private readonly DispatcherTimer _progressTimer;
    private bool _isDisposed;

    private string _selectedGenre = AllGenres;
    private string _searchText = string.Empty;
    private int _minRating;
    private TrackReaction? _reactionFilter;
    private MainViewMode _activeView = MainViewMode.Tracks;
    private IReadOnlyList<AlbumAggregate> _displayedAlbums = Array.Empty<AlbumAggregate>();
    private IReadOnlyList<ArtistAggregate> _displayedArtists = Array.Empty<ArtistAggregate>();
    private readonly Dictionary<int, ObservableCollection<Tag>> _tagsByTrackId = new();
    private readonly ObservableCollection<int> _selectedTagIds = new();
    private readonly ObservableCollection<TagFilterItem> _tagFilters = new();
    private Track? _selectedTrack;
    private Track? _playingTrack;
    private string _statusMessage = "Выберите трек для воспроизведения.";
    private OperationMessageKind _statusKind = OperationMessageKind.Info;
    private bool _isPlaying;
    private bool _isPaused;
    private Track? _pendingHistoryTrack;
    private TimeSpan _currentPosition;
    private TimeSpan _currentDuration;
    private RepeatMode _repeatMode;
    private double _volume = 1.0;
    private bool _isMuted;
    private bool _isSeeking;
    private readonly Stack<LeftColumnState> _navStack = new();
    private LeftColumnState _currentLeftColumn = new LeftColumnState.TracksRoot();
    private static readonly System.Random _shuffleRng = new();

    // Очередь воспроизведения — ОТДЕЛЬНА от DisplayedTracks (вкладка «Треки»).
    // PlayAlbum/PlayArtist/PlayDetailTrack заполняют её треками альбома/исполнителя, не трогая
    // список вкладки «Треки». Обычное воспроизведение из вкладки «Треки» (PlayOrPause/PlaySpecificTrack/
    // history replay) снимает снимок текущего DisplayedTracks в очередь — так next/prev продолжают
    // ходить по видимой библиотеке, как и раньше. См. docs/superpowers/plans (smoke-fix Task «playback queue»).
    private List<Track> _playbackQueue = new();
    private int _queueIndex = -1;

    public MainViewModel(
        ITrackRepository trackRepository,
        IFileService fileService,
        ISaveFileDialogService saveFileDialogService,
        IAudioPlayerService audioPlayerService,
        IListeningHistoryRepository listeningHistoryRepository,
        IPlayerSettingsRepository playerSettingsRepository,
        IAddTrackDialogService? addTrackDialogService = null,
        IConfirmationService? confirmationService = null,
        ISearchService? searchService = null,
        IStatsWindowService? statsWindowService = null,
        ITagRepository? tagRepository = null,
        ITagsWindowService? tagsWindowService = null,
        PlaylistsViewModel? playlists = null,
        IMusicStoragePaths? storagePaths = null)
    {
        _trackRepository = trackRepository;
        _fileService = fileService;
        _storagePaths = storagePaths;
        _saveFileDialogService = saveFileDialogService;
        _audioPlayerService = audioPlayerService;
        _listeningHistoryRepository = listeningHistoryRepository;
        _playerSettingsRepository = playerSettingsRepository;
        _addTrackDialogService = addTrackDialogService;
        _confirmationService = confirmationService;
        _searchService = searchService;
        _statsWindowService = statsWindowService;
        _tagsWindowService = tagsWindowService;
        _tagRepository = tagRepository;
        _playlists = playlists;
        _selectedTagIds.CollectionChanged += (_, _) => ApplyFilters();
        _allTracks = new List<Track>(trackRepository.GetAll());
        LoadTagFilters();

        DisplayedTracks = new ObservableCollection<Track>(_allTracks);
        // Начальные агрегаты считаем от полного списка треков (фильтров ещё нет).
        DisplayedAlbums = LibraryGroupingService.GroupByAlbum(_allTracks);
        DisplayedArtists = LibraryGroupingService.GroupByArtist(_allTracks);
        PlaybackHistory = new ObservableCollection<PlaybackEntry>();
        Genres = new ObservableCollection<string>(
            new[] { AllGenres }
                .Concat(_allTracks.Select(track => track.Genre).Distinct().OrderBy(genre => genre)));

        foreach (var entry in _listeningHistoryRepository.GetRecent(MaxHistoryItems))
        {
            PlaybackHistory.Add(entry);
        }

        PlayPauseCommand = CreateCommand(_ => PlayOrPause(), _ => SelectedTrack is not null || PlayingTrack is not null);
        TogglePlaybackCommand = CreateCommand(_ => PlayOrPause(PlayingTrack ?? SelectedTrack), _ => PlayingTrack is not null || SelectedTrack is not null);
        StopCommand = CreateCommand(_ => Stop(), _ => PlayingTrack is not null);
        SaveTrackCommand = CreateCommand(_ => SaveSelectedTrack(), _ => SelectedTrack is not null);
        AddTrackCommand = CreateCommand(_ => OpenAddTrackDialog(), _ => _addTrackDialogService is not null);
        DeleteTrackCommand = CreateCommand(_ => DeleteSelectedTrack(), _ => CanDeleteSelected);
        PlayTrackCommand = CreateCommand(
            parameter => PlaySpecificTrack(parameter as Track),
            parameter => parameter is Track);
        ReplayHistoryEntryCommand = CreateCommand(
            parameter => ReplayHistoryEntry(parameter as PlaybackEntry),
            parameter => parameter is PlaybackEntry);
        OpenStatsCommand = CreateCommand(
            _ => _statsWindowService?.Show(),
            _ => _statsWindowService is not null);
        OpenTagsCommand = CreateCommand(
            _ => _tagsWindowService?.Show(),
            _ => _tagsWindowService is not null);
        // Рейтинг и реакция — пользовательская оценка, разрешена и для встроенных
        // треков (UI ограничивает только удаление seed-треков, не их оценку).
        SetRatingCommand = CreateCommand(
            parameter => SetRatingOnSelected(parameter),
            _ => SelectedTrack is not null);
        SetReactionCommand = CreateCommand(
            parameter => SetReactionOnSelected(parameter),
            _ => SelectedTrack is not null);
        ToggleTagFilterCommand = CreateCommand(parameter => ToggleTagFilter(parameter as TagFilterItem));
        RefreshTagFiltersCommand = CreateCommand(_ => { LoadTagFilters(); ApplyFilters(); });
        ClearFiltersCommand = CreateCommand(_ => ClearFilters());

        // Прикрепляем тег к выбранному треку и обновляем кэш.
        AttachTagToSelectedCommand = CreateCommand(parameter =>
        {
            if (SelectedTrack is null || _tagRepository is null) return;
            if (parameter is not Tag tag) return;
            if (_tagsByTrackId.TryGetValue(SelectedTrack.Id, out var existing) &&
                existing.Any(t => t.Id == tag.Id)) return;
            _tagRepository.AttachTag(SelectedTrack.Id, tag.Id);
            if (!_tagsByTrackId.ContainsKey(SelectedTrack.Id))
                _tagsByTrackId[SelectedTrack.Id] = new ObservableCollection<Tag>();
            _tagsByTrackId[SelectedTrack.Id].Add(tag);
            OnPropertyChanged(nameof(AvailableTagsForAttach));
            ApplyFilters();
        });

        // Открепляем тег от выбранного трека и обновляем кэш.
        DetachTagFromSelectedCommand = CreateCommand(parameter =>
        {
            if (SelectedTrack is null || _tagRepository is null) return;
            if (parameter is not Tag tag) return;
            _tagRepository.DetachTag(SelectedTrack.Id, tag.Id);
            if (_tagsByTrackId.TryGetValue(SelectedTrack.Id, out var collection))
            {
                var existingTag = collection.FirstOrDefault(t => t.Id == tag.Id);
                if (existingTag is not null) collection.Remove(existingTag);
            }
            OnPropertyChanged(nameof(AvailableTagsForAttach));
            ApplyFilters();
        });
        SetReactionFilterCommand = CreateCommand(parameter => SetReactionFilter(parameter));
        SetMinRatingCommand = CreateCommand(parameter =>
        {
            if (!TryParseInt(parameter, out int requested)) return;
            // Toggle: клик по уже активной звезде → 0 («Все»).
            // Сравниваем по зажатому значению: Execute("9") дважды = 5 → 0.
            int clamped = Math.Clamp(requested, 0, 5);
            MinRating = MinRating == clamped ? 0 : clamped;
        });

        SkipForwardCommand = CreateCommand(_ => SkipBy(TimeSpan.FromSeconds(10)), _ => PlayingTrack is not null);
        SkipBackwardCommand = CreateCommand(_ => SkipBy(TimeSpan.FromSeconds(-10)), _ => PlayingTrack is not null);
        PreviousTrackCommand = CreateCommand(_ => GoToTrackByOffset(-1), _ => CanGoToOffset(-1));
        NextTrackCommand = CreateCommand(_ => GoToTrackByOffset(+1), _ => CanGoToOffset(+1));
        ToggleMuteCommand = CreateCommand(_ => IsMuted = !IsMuted);
        CycleRepeatModeCommand = CreateCommand(_ => CycleRepeatMode());
        SeekToCommand = CreateCommand(parameter => SeekTo(parameter as TimeSpan?), _ => PlayingTrack is not null);

        // Навигация левой колонки: SwitchView сбрасывает back-стек и переустанавливает корень,
        // OpenAlbum/OpenArtist пушат текущее состояние перед drill-down, Back — снимает верхнее.
        SwitchViewCommand = CreateCommand(p =>
        {
            if (p is not MainViewMode mode) return;
            ActiveView = mode;
            _navStack.Clear();
            CurrentLeftColumn = mode switch
            {
                MainViewMode.Tracks => new LeftColumnState.TracksRoot(),
                MainViewMode.Albums => new LeftColumnState.AlbumsRoot(),
                MainViewMode.Artists => new LeftColumnState.ArtistsRoot(),
                MainViewMode.Playlists => new LeftColumnState.PlaylistsRoot(),
                _ => new LeftColumnState.TracksRoot()
            };
            OnPropertyChanged(nameof(CanGoBack));
        });

        OpenAlbumCommand = CreateCommand(p =>
        {
            if (p is not AlbumAggregate album) return;
            _navStack.Push(_currentLeftColumn);
            CurrentLeftColumn = new LeftColumnState.AlbumDetail(album);
            OnPropertyChanged(nameof(CanGoBack));
        });

        OpenArtistCommand = CreateCommand(p =>
        {
            if (p is not ArtistAggregate artist) return;
            _navStack.Push(_currentLeftColumn);
            CurrentLeftColumn = new LeftColumnState.ArtistDetail(artist);
            OnPropertyChanged(nameof(CanGoBack));
        });

        BackCommand = CreateCommand(_ =>
        {
            if (_navStack.Count == 0) return;
            CurrentLeftColumn = RefreshDetail(_navStack.Pop());
            OnPropertyChanged(nameof(CanGoBack));
        }, _ => _navStack.Count > 0);

        PlayAlbumCommand = CreateCommand(p =>
        {
            if (p is not AlbumAggregate album || album.Tracks.Count == 0) return;
            ReplaceQueueAndPlay(album.Tracks, shuffle: false);
        });

        ShuffleAlbumCommand = CreateCommand(p =>
        {
            if (p is not AlbumAggregate album || album.Tracks.Count == 0) return;
            ReplaceQueueAndPlay(album.Tracks, shuffle: true);
        });

        PlayArtistCommand = CreateCommand(p =>
        {
            if (p is not ArtistAggregate artist) return;
            var all = artist.Albums.SelectMany(a => a.Tracks).Concat(artist.LooseTracks).ToList();
            if (all.Count == 0) return;
            ReplaceQueueAndPlay(all, shuffle: false);
        });

        ShuffleArtistCommand = CreateCommand(p =>
        {
            if (p is not ArtistAggregate artist) return;
            var all = artist.Albums.SelectMany(a => a.Tracks).Concat(artist.LooseTracks).ToList();
            if (all.Count == 0) return;
            ReplaceQueueAndPlay(all, shuffle: true);
        });

        // Воспроизведение трека, кликнутого прямо на detail-странице альбома/исполнителя (bug #4).
        // Очередь строится из треклиста ИМЕННО той детали, что сейчас открыта в CurrentLeftColumn —
        // не из DisplayedTracks (которая продолжает показывать вкладку «Треки»).
        PlayDetailTrackCommand = CreateCommand(p =>
        {
            if (p is not Track track) return;
            IReadOnlyList<Track>? queueSource = _currentLeftColumn switch
            {
                LeftColumnState.AlbumDetail albumDetail => albumDetail.Album.Tracks,
                LeftColumnState.ArtistDetail artistDetail => artistDetail.Artist.LooseTracks,
                _ => null
            };
            if (queueSource is null || queueSource.Count == 0) return;
            ReplaceQueueAndPlay(queueSource, shuffle: false, selectedTrack: track);
        });

        // Плейлисты (ADR-0002): CRUD/членство живёт в PlaylistsViewModel, здесь — только
        // навигация в CurrentLeftColumn и воспроизведение через общий ReplaceQueueAndPlay.
        OpenPlaylistCommand = CreateCommand(p =>
        {
            if (p is not Playlist pl || _playlists is null) return;
            _navStack.Push(_currentLeftColumn);
            CurrentLeftColumn = new LeftColumnState.PlaylistDetail(_playlists.BuildView(pl.Id));
            OnPropertyChanged(nameof(CanGoBack));
        });
        PlayPlaylistCommand = CreateCommand(p =>
        {
            if (p is not PlaylistView v || v.Tracks.Count == 0) return;
            ReplaceQueueAndPlay(v.Tracks, shuffle: false);
        });
        ShufflePlaylistCommand = CreateCommand(p =>
        {
            if (p is not PlaylistView v || v.Tracks.Count == 0) return;
            ReplaceQueueAndPlay(v.Tracks, shuffle: true);
        });
        PlayPlaylistTrackCommand = CreateCommand(p =>
        {
            if (p is not Track track || _currentLeftColumn is not LeftColumnState.PlaylistDetail d) return;
            ReplaceQueueAndPlay(d.Playlist.Tracks, shuffle: false, selectedTrack: track);
        });

        // Контекст-меню (Task 7): «Добавить в плейлист ▸» на любой трек-строке и «Убрать из
        // плейлиста» в PlaylistDetail. Фиксацию контекст-таргета (какой трек/выделение) делает
        // TrackContextMenuBehavior при открытии меню — команды здесь только применяют мутацию.
        // Ре-резолв открытой детали НЕ вызывается вручную: под-VM поднимает PlaylistContentChanged,
        // обработчик OnPlaylistContentChanged (подписан ниже) сам пересобирает деталь.
        AddContextToPlaylistCommand = CreateCommand(p =>
        {
            if (_playlists is null || p is not Playlist pl) return;
            _playlists.AddContextTargetToPlaylist(pl);
        });
        RemovePlaylistTrackCommand = CreateCommand(p =>
        {
            if (_playlists is null || _currentLeftColumn is not LeftColumnState.PlaylistDetail d) return;
            if (p is not int position) return;
            _playlists.RemoveTrackAt(d.Playlist.Playlist.Id, position);
        });

        // DnD (Task 8): добавление треков дропом на строку плейлиста / в треклист детали, и
        // reorder внутри треклиста детали. Параметр — value-tuple, собирается в
        // DragDropListHelper по факту Drop (позиция/id уже посчитаны там через ReorderMath —
        // здесь только применение к под-VM). Ре-резолв — тем же событийным путём
        // (PlaylistContentChanged → OnPlaylistContentChanged), вручную не зовём.
        DropTracksOnPlaylistCommand = CreateCommand(p =>
        {
            if (_playlists is null || p is not (Playlist pl, IReadOnlyList<int> ids)) return;
            _playlists.AddTracksByIds(pl.Id, ids);
        });
        ReorderPlaylistCommand = CreateCommand(p =>
        {
            if (_playlists is null || _currentLeftColumn is not LeftColumnState.PlaylistDetail d) return;
            if (p is not (int from, int to)) return;
            _playlists.Reorder(d.Playlist.Playlist.Id, from, to);
        });

        // Ре-резолв открытой детали плейлиста после мутации членства (add/remove/reorder) —
        // подписка один раз здесь, отписка в Dispose. См. PlaylistsViewModel.PlaylistContentChanged.
        if (_playlists is not null)
        {
            _playlists.PlaylistContentChanged += OnPlaylistContentChanged;
            _playlists.PropertyChanged += OnPlaylistsPropertyChanged;
        }

        _audioPlayerService.MediaOpened += OnMediaOpened;
        _audioPlayerService.MediaEnded += OnMediaEnded;
        _audioPlayerService.MediaFailed += OnMediaFailed;

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _progressTimer.Tick += OnProgressTick;
    }

    public ObservableCollection<Track> DisplayedTracks { get; }
    public ObservableCollection<string> Genres { get; }
    public ObservableCollection<PlaybackEntry> PlaybackHistory { get; }

    /// <summary>
    /// Текущая очередь воспроизведения (read-only снимок для тестов/диагностики).
    /// Заполняется ReplaceQueueAndPlay (альбом/исполнитель/detail-трек) или снимком
    /// DisplayedTracks при обычном воспроизведении из вкладки «Треки». next/prev и
    /// auto-advance по завершении трека ходят по ней, а не по DisplayedTracks.
    /// </summary>
    public IReadOnlyList<Track> PlaybackQueue => _playbackQueue;

    public ICommand PlayPauseCommand { get; }
    public ICommand TogglePlaybackCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveTrackCommand { get; }
    public ICommand AddTrackCommand { get; }
    public ICommand DeleteTrackCommand { get; }
    public ICommand PlayTrackCommand { get; }
    public ICommand ReplayHistoryEntryCommand { get; }
    public ICommand OpenStatsCommand { get; }
    public ICommand OpenTagsCommand { get; }
    public ICommand SetRatingCommand { get; }
    public ICommand SetReactionCommand { get; }
    public ICommand ToggleTagFilterCommand { get; }
    public ICommand RefreshTagFiltersCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand SetReactionFilterCommand { get; }
    public ICommand SetMinRatingCommand { get; }
    public ICommand AttachTagToSelectedCommand { get; }
    public ICommand DetachTagFromSelectedCommand { get; }
    public ICommand SwitchViewCommand { get; }
    public ICommand OpenAlbumCommand { get; }
    public ICommand OpenArtistCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand PlayAlbumCommand { get; }
    public ICommand ShuffleAlbumCommand { get; }
    public ICommand PlayArtistCommand { get; }
    public ICommand ShuffleArtistCommand { get; }
    public ICommand PlayDetailTrackCommand { get; }
    public ICommand OpenPlaylistCommand { get; }
    public ICommand PlayPlaylistCommand { get; }
    public ICommand ShufflePlaylistCommand { get; }
    public ICommand PlayPlaylistTrackCommand { get; }
    public ICommand AddContextToPlaylistCommand { get; }
    public ICommand RemovePlaylistTrackCommand { get; }
    public ICommand DropTracksOnPlaylistCommand { get; }
    public ICommand ReorderPlaylistCommand { get; }

    /// <summary>Под-VM плейлистов (ADR-0002). Доступна только когда MainViewModel сконструирован с ней (DI); в старых тестах — null-guard в командах.</summary>
    public PlaylistsViewModel Playlists => _playlists!;

    /// <summary>Кэш тегов по треку. XAML-конвертер использует его для отображения чипов на карточке трека.</summary>
    public IReadOnlyDictionary<int, ObservableCollection<Tag>> TagsByTrackId => _tagsByTrackId;

    /// <summary>
    /// Теги, доступные для прикрепления к выбранному треку (глобальный список минус уже прикреплённые).
    /// Пересчитывается при смене SelectedTrack и при каждой операции attach/detach.
    /// </summary>
    public IEnumerable<Tag> AvailableTagsForAttach
    {
        get
        {
            if (SelectedTrack is null) return Array.Empty<Tag>();
            var attached = _tagsByTrackId.TryGetValue(SelectedTrack.Id, out var c)
                ? new HashSet<int>(c.Select(t => t.Id))
                : new HashSet<int>();
            return _tagFilters.Where(item => !attached.Contains(item.Tag.Id)).Select(item => item.Tag).ToList();
        }
    }

    /// <summary>Чипы тегов для левой колонки. Обновляются вручную через RefreshTagFiltersCommand.</summary>
    public ObservableCollection<TagFilterItem> TagFilters => _tagFilters;
    public ICommand SkipForwardCommand { get; }
    public ICommand SkipBackwardCommand { get; }
    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand CycleRepeatModeCommand { get; }
    public ICommand SeekToCommand { get; }

    public string SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            if (SetProperty(ref _selectedGenre, value))
            {
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Свободная строка поиска. Дебаунс делается на стороне XAML через
    /// Binding Delay=250, поэтому setter ViewModel-а просто применяет фильтр
    /// синхронно — это упрощает unit-тесты (без таймера).
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplyFilters();
            }
        }
    }

    /// <summary>Минимальный рейтинг трека для фильтра (0..5). 0 = фильтр не активен.</summary>
    public int MinRating
    {
        get => _minRating;
        set
        {
            int clamped = Math.Clamp(value, 0, 5);
            if (SetProperty(ref _minRating, clamped))
            {
                OnPropertyChanged(nameof(MinRatingDisplayText));
                ApplyFilters();
            }
        }
    }

    /// <summary>Подпись слайдера рейтинга: «Без фильтра» или «≥ N★».</summary>
    public string MinRatingDisplayText =>
        _minRating <= 0 ? "Без фильтра" : $"≥ {_minRating}★";

    /// <summary>Фильтр по реакции. null = «любая реакция».</summary>
    public TrackReaction? ReactionFilter
    {
        get => _reactionFilter;
        set
        {
            if (SetProperty(ref _reactionFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    /// <summary>Идентификаторы тегов, активных в фильтре (OR-семантика). UI добавляет/удаляет через эту коллекцию.</summary>
    public ObservableCollection<int> SelectedTagIds => _selectedTagIds;

    /// <summary>Активный режим левой колонки (треки/альбомы/исполнители). Persisted в KeyValueStore.</summary>
    public MainViewMode ActiveView
    {
        get => _activeView;
        set
        {
            if (SetProperty(ref _activeView, value))
            {
                _playerSettingsRepository?.SaveActiveView(value);
            }
        }
    }

    /// <summary>Computed-проекция отфильтрованных треков по альбомам (см. LibraryGroupingService). Пересчитывается в ApplyFilters.</summary>
    public IReadOnlyList<AlbumAggregate> DisplayedAlbums
    {
        get => _displayedAlbums;
        private set
        {
            if (!ReferenceEquals(_displayedAlbums, value))
            {
                _displayedAlbums = value;
                OnPropertyChanged(nameof(DisplayedAlbums));
            }
        }
    }

    /// <summary>Computed-проекция отфильтрованных треков по исполнителям (см. LibraryGroupingService). Пересчитывается в ApplyFilters.</summary>
    public IReadOnlyList<ArtistAggregate> DisplayedArtists
    {
        get => _displayedArtists;
        private set
        {
            if (!ReferenceEquals(_displayedArtists, value))
            {
                _displayedArtists = value;
                OnPropertyChanged(nameof(DisplayedArtists));
            }
        }
    }

    /// <summary>
    /// Текущее состояние левой колонки (drill-down: root/album-detail/artist-detail).
    /// Меняется командами SwitchView/OpenAlbum/OpenArtist/Back.
    /// </summary>
    public LeftColumnState CurrentLeftColumn
    {
        get => _currentLeftColumn;
        private set
        {
            if (!Equals(_currentLeftColumn, value))
            {
                _currentLeftColumn = value;
                OnPropertyChanged(nameof(CurrentLeftColumn));
            }
        }
    }

    /// <summary>Есть ли куда возвращаться по Back (back-стек непуст).</summary>
    public bool CanGoBack => _navStack.Count > 0;

    public Track? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (SetProperty(ref _selectedTrack, value))
            {
                OnPropertyChanged(nameof(HasSelectedTrack));
                OnPropertyChanged(nameof(IsSelectedPlaying));
                OnPropertyChanged(nameof(PlayPauseText));
                OnPropertyChanged(nameof(ShowOtherPlayingBadge));
                OnPropertyChanged(nameof(OtherPlayingText));
                OnPropertyChanged(nameof(CanDeleteSelected));
                OnPropertyChanged(nameof(AvailableTagsForAttach));
                // NowPlayingTrack падает обратно на SelectedTrack, когда ничего не играет —
                // поэтому смена выделения тоже должна перерисовать обложку/название в средней панели.
                OnPropertyChanged(nameof(NowPlayingTrack));
                OnPropertyChanged(nameof(HasNowPlayingTrack));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public Track? PlayingTrack
    {
        get => _playingTrack;
        private set
        {
            if (SetProperty(ref _playingTrack, value))
            {
                OnPropertyChanged(nameof(IsSelectedPlaying));
                OnPropertyChanged(nameof(PlayPauseText));
                OnPropertyChanged(nameof(ShowOtherPlayingBadge));
                OnPropertyChanged(nameof(OtherPlayingText));
                OnPropertyChanged(nameof(NowPlayingTrack));
                OnPropertyChanged(nameof(HasNowPlayingTrack));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// Трек для отображения в средней «now playing» панели (обложка/название/исполнитель/жанр/
    /// длительность). Приоритет — играющий трек: он может не входить в текущий DisplayedTracks
    /// (например, после Play-альбома), и тогда WPF-биндинг ListBox.SelectedItem (TwoWay) обнулит
    /// SelectedTrack, потому что элемент ушёл из ItemsSource — сама средняя панель раньше была
    /// привязана к SelectedTrack и из-за этого гасла (bug #6). Если ничего не играет — падаем на
    /// SelectedTrack, чтобы клик по треку в списке по-прежнему показывал его карточку до старта плеера.
    /// </summary>
    public Track? NowPlayingTrack => PlayingTrack ?? SelectedTrack;

    public bool HasNowPlayingTrack => NowPlayingTrack is not null;

    public bool HasSelectedTrack => SelectedTrack is not null;

    public bool CanDeleteSelected =>
        SelectedTrack is not null && IsUserTrack(SelectedTrack);

    private static bool IsUserTrack(Track track) => !track.IsBuiltIn;

    public bool IsSelectedPlaying =>
        SelectedTrack is not null
        && PlayingTrack is not null
        && SelectedTrack.Id == PlayingTrack.Id;

    public bool ShowOtherPlayingBadge =>
        PlayingTrack is not null
        && (SelectedTrack is null || SelectedTrack.Id != PlayingTrack.Id);

    public string OtherPlayingText =>
        PlayingTrack is null ? string.Empty : $"▶ играет: {PlayingTrack.Artist} — {PlayingTrack.Title}";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public OperationMessageKind StatusKind
    {
        get => _statusKind;
        private set => SetProperty(ref _statusKind, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseText));
            }
        }
    }

    public string PlayPauseText => IsSelectedPlaying && _isPlaying ? "Пауза" : "Воспроизвести";

    public RepeatMode RepeatMode
    {
        get => _repeatMode;
        set
        {
            if (SetProperty(ref _repeatMode, value))
            {
                PersistSettings();
            }
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            // Жёсткий clamp перед записью в плеер — UI слайдеру удобнее не знать про границы.
            if (!double.IsFinite(value)) return;
            double clamped = Math.Clamp(value, 0.0, 1.0);
            if (SetProperty(ref _volume, clamped))
            {
                _audioPlayerService.Volume = clamped;
                PersistSettings();
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                _audioPlayerService.IsMuted = value;
                PersistSettings();
            }
        }
    }

    public bool IsSeeking
    {
        get => _isSeeking;
        set => SetProperty(ref _isSeeking, value);
    }

    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        private set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                OnPropertyChanged(nameof(CurrentPositionText));
                OnPropertyChanged(nameof(ProgressValue));
            }
        }
    }

    public TimeSpan CurrentDuration
    {
        get => _currentDuration;
        private set
        {
            if (SetProperty(ref _currentDuration, value))
            {
                OnPropertyChanged(nameof(CurrentDurationText));
                OnPropertyChanged(nameof(ProgressMaximum));
            }
        }
    }

    public string CurrentPositionText => PlaybackTime.Format(CurrentPosition);
    public string CurrentDurationText => PlaybackTime.Format(CurrentDuration);
    public double ProgressValue => CurrentPosition.TotalSeconds;
    public double ProgressMaximum => Math.Max(CurrentDuration.TotalSeconds, 1);

    public int GetNextTrackId()
    {
        return _allTracks.Count == 0 ? 1 : _allTracks.Max(track => track.Id) + 1;
    }

    private void OpenAddTrackDialog()
    {
        if (_addTrackDialogService is null)
        {
            return;
        }

        Track? saved = _addTrackDialogService.Show(candidate => _trackRepository.Add(new Track
        {
            Title = candidate.Title,
            Artist = candidate.Artist,
            Album = candidate.Album,
            Genre = candidate.Genre,
            Duration = candidate.Duration,
            FilePath = candidate.AudioFilePath,
            CoverPath = candidate.CoverFilePath,
            IsBuiltIn = false,
            Year = candidate.Year,
            TrackNumber = candidate.TrackNumber,
            AlbumArtist = candidate.AlbumArtist
        }));
        if (saved is null) return;
        AddTrack(saved);
        SetStatus(OperationResult.Success($"Трек «{saved.Title}» добавлен в библиотеку."));
    }

    private void DeleteSelectedTrack()
    {
        Track? track = SelectedTrack;
        if (track is null || !IsUserTrack(track))
        {
            return;
        }

        if (_confirmationService is not null)
        {
            bool confirmed = _confirmationService.Confirm(
                "Удалить трек",
                $"Удалить «{track.Artist} — {track.Title}» из библиотеки? Импортированная копия и её обложка будут удалены. Исходники вне папки библиотеки сохранятся.");
            if (!confirmed)
            {
                return;
            }
        }

        if (PlayingTrack is not null && PlayingTrack.Id == track.Id)
        {
            ResetPlaybackState();
        }

        _trackRepository.Remove(track.Id);
        var audioResult = DeleteManagedTrackFile(track.FilePath, _storagePaths?.MusicDirectory, track.Id);
        var coverResult = DeleteManagedTrackFile(track.CoverPath, _storagePaths?.CoversDirectory, track.Id);
        RemoveTrack(track);
        SetStatus(!audioResult.IsSuccess ? audioResult : !coverResult.IsSuccess ? coverResult
            : OperationResult.Success($"Трек «{track.Title}» удалён из библиотеки."));
    }

    private OperationResult DeleteManagedTrackFile(string path, string? directory, int trackId)
    {
        if (directory is null || string.IsNullOrWhiteSpace(path)
            || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || _allTracks.Any(t => t.Id != trackId && ((!string.IsNullOrWhiteSpace(t.FilePath) && IsSamePath(t.FilePath, path)) || (!string.IsNullOrWhiteSpace(t.CoverPath) && IsSamePath(t.CoverPath, path)))))
            return OperationResult.Info("Исходный или общий файл сохранён.");
        var result = _fileService.Delete(path);
        return result.IsSuccess ? result : OperationResult.Error($"Запись удалена, но файл остался: {result.Message}");
    }

    private void RemoveTrack(Track track)
    {
        _allTracks.RemoveAll(t => t.Id == track.Id);
        int removedBefore = _playbackQueue.Take(Math.Max(_queueIndex, 0)).Count(t => t.Id == track.Id);
        _playbackQueue.RemoveAll(t => t.Id == track.Id);
        _queueIndex -= removedBefore;
        for (int i = PlaybackHistory.Count - 1; i >= 0; i--)
            if (PlaybackHistory[i].Track.Id == track.Id) PlaybackHistory.RemoveAt(i);

        // Если последний трек этого жанра ушёл — убираем жанр из фильтра.
        if (!_allTracks.Any(t => t.Genre == track.Genre) && Genres.Contains(track.Genre))
        {
            Genres.Remove(track.Genre);
            if (SelectedGenre == track.Genre)
            {
                SelectedGenre = AllGenres;
            }
        }

        if (SelectedTrack?.Id == track.Id)
        {
            SelectedTrack = null;
        }

        // Удаляем запись кэша тегов вместе с треком.
        _tagsByTrackId.Remove(track.Id);
        ApplyFilters();
        CommandManager.InvalidateRequerySuggested();
    }

    private void SetRatingOnSelected(object? parameter)
    {
        if (SelectedTrack is null) return;
        if (!TryParseInt(parameter, out int requested)) return;
        // Toggle: повторный клик по той же звезде сбрасывает рейтинг в 0.
        int next = SelectedTrack.Rating == requested ? 0 : Math.Clamp(requested, 0, 5);
        UpdateSelectedTrack(CloneTrack(SelectedTrack, rating: next));
    }

    private void SetReactionOnSelected(object? parameter)
    {
        if (SelectedTrack is null) return;
        TrackReaction requested = parameter switch
        {
            TrackReaction r => r,
            string s when Enum.TryParse<TrackReaction>(s, ignoreCase: true, out var parsed) => parsed,
            _ => TrackReaction.None
        };
        // Toggle: повторный клик по уже активной реакции сбрасывает в None.
        TrackReaction next = SelectedTrack.Reaction == requested ? TrackReaction.None : requested;
        UpdateSelectedTrack(CloneTrack(SelectedTrack, reaction: next));
    }

    // Track — sealed class с init-only свойствами, `with` для него не работает,
    // поэтому копируем вручную. Если в будущем Track станет record — можно убрать.
    private static Track CloneTrack(Track source, int? rating = null, TrackReaction? reaction = null) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Artist = source.Artist,
        Album = source.Album,
        Genre = source.Genre,
        Duration = source.Duration,
        FilePath = source.FilePath,
        CoverPath = source.CoverPath,
        Rating = rating ?? source.Rating,
        Reaction = reaction ?? source.Reaction,
        IsBuiltIn = source.IsBuiltIn,
        Year = source.Year,
        TrackNumber = source.TrackNumber,
        AlbumArtist = source.AlbumArtist
    };

    private void UpdateSelectedTrack(Track updated)
    {
        // Капчуем намерение ДО мутации коллекций. Иначе после
        // DisplayedTracks[i] = updated ListBox увидит Remove+Add, не найдёт
        // старый объект SelectedItem в коллекции и сбросит SelectedTrack в null
        // через TwoWay-биндинг — после этого мы уже не сможем понять, нужно ли
        // восстанавливать выделение.
        bool wasSelected = SelectedTrack?.Id == updated.Id;
        bool wasPlaying = PlayingTrack?.Id == updated.Id;

        _trackRepository.Update(updated);

        int allIndex = _allTracks.FindIndex(t => t.Id == updated.Id);
        if (allIndex >= 0) _allTracks[allIndex] = updated;

        int displayedIndex = -1;
        for (int i = 0; i < DisplayedTracks.Count; i++)
        {
            if (DisplayedTracks[i].Id == updated.Id) { displayedIndex = i; break; }
        }
        if (displayedIndex >= 0) DisplayedTracks[displayedIndex] = updated;

        if (wasSelected) SelectedTrack = updated;
        if (wasPlaying) PlayingTrack = updated;
        for (int i = 0; i < _playbackQueue.Count; i++)
            if (_playbackQueue[i].Id == updated.Id) _playbackQueue[i] = updated;
        ApplyFilters();
    }

    private void LoadTagFilters()
    {
        if (_tagRepository is null) return;
        var fresh = _tagRepository.GetAll();
        var associations = _tagRepository.GetTagsByTrack();
        foreach (var track in _allTracks)
        {
            if (!_tagsByTrackId.TryGetValue(track.Id, out var tags))
                _tagsByTrackId[track.Id] = tags = new ObservableCollection<Tag>();
            tags.Clear();
            if (associations.TryGetValue(track.Id, out var current))
                foreach (var tag in current) tags.Add(tag);
        }
        OnPropertyChanged(nameof(AvailableTagsForAttach));
        var stillSelected = new HashSet<int>(_tagFilters.Where(f => f.IsSelected).Select(f => f.Tag.Id));
        _tagFilters.Clear();
        foreach (var tag in fresh)
        {
            _tagFilters.Add(new TagFilterItem(tag, stillSelected.Contains(tag.Id)));
        }
        // Если какой-то тег был удалён, убираем его id из активного фильтра.
        var freshIds = new HashSet<int>(fresh.Select(t => t.Id));
        for (int i = _selectedTagIds.Count - 1; i >= 0; i--)
        {
            if (!freshIds.Contains(_selectedTagIds[i]))
            {
                _selectedTagIds.RemoveAt(i);
            }
        }
    }

    private void ToggleTagFilter(TagFilterItem? item)
    {
        if (item is null) return;
        item.IsSelected = !item.IsSelected;
        if (item.IsSelected)
        {
            if (!_selectedTagIds.Contains(item.Tag.Id))
            {
                _selectedTagIds.Add(item.Tag.Id);
            }
        }
        else
        {
            _selectedTagIds.Remove(item.Tag.Id);
        }
    }

    private void SetReactionFilter(object? parameter)
    {
        // null/«Any» → выключить фильтр; «Liked»/«Disliked» → включить.
        // Повторный клик по активной кнопке сбрасывает в null (как и SetReactionCommand).
        if (parameter is null || parameter is string s1 && string.Equals(s1, "Any", StringComparison.OrdinalIgnoreCase))
        {
            ReactionFilter = null;
            return;
        }
        TrackReaction requested = parameter switch
        {
            TrackReaction r => r,
            string s when Enum.TryParse<TrackReaction>(s, ignoreCase: true, out var parsed) => parsed,
            _ => TrackReaction.None
        };
        ReactionFilter = ReactionFilter == requested ? null : requested;
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedGenre = AllGenres;
        MinRating = 0;
        ReactionFilter = null;
        foreach (var item in _tagFilters) item.IsSelected = false;
        _selectedTagIds.Clear();
    }

    private static bool TryParseInt(object? parameter, out int value)
    {
        if (parameter is int i) { value = i; return true; }
        if (parameter is string s &&
            int.TryParse(s, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    public void AddTrack(Track track)
    {
        _allTracks.Add(track);

        if (!Genres.Contains(track.Genre))
        {
            // Жанры отсортированы по алфавиту, «Все жанры» всегда первый.
            int insertIndex = 1;
            while (insertIndex < Genres.Count && string.CompareOrdinal(Genres[insertIndex], track.Genre) < 0)
            {
                insertIndex++;
            }
            Genres.Insert(insertIndex, track.Genre);
        }

        // Новый трек ещё не имеет тегов — инициализируем пустую коллекцию в кэше.
        _tagsByTrackId[track.Id] = new ObservableCollection<Tag>();
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        try { ApplyFiltersCore(); }
        catch (Exception ex) when (Services.StorageErrors.IsExpected(ex)) { ReportStorageError("Не удалось обновить библиотеку", ex); }
    }

    private void ApplyFiltersCore()
    {
        // Собираем снимок активных критериев и отдаём в чистую функцию LibraryFilter.
        // Это держит метод тонким и позволяет юнит-тестам фильтра жить без WPF.
        IReadOnlyList<Track>? hits = null;
        if (!string.IsNullOrWhiteSpace(SearchText) && _searchService is not null)
        {
            // Все кандидаты нужны ДО фильтрации: LIMIT 500 скрывал совпадения других жанров/тегов.
            hits = _searchService.Search(SearchText, Math.Max(_allTracks.Count, 1));
        }

        var criteria = new LibraryFilterCriteria(
            hits,
            SelectedGenre == AllGenres ? null : SelectedGenre,
            MinRating,
            ReactionFilter,
            _selectedTagIds);

        // Адаптер для LibraryFilter: ему нужен делегат «id → список тегов-id»,
        // чтобы не тащить ITagRepository в pure-функцию. Если репозитория нет
        // (старые unit-тесты с null), фильтр по тегам в criteria всё равно
        // не активируется (пустой SelectedTagIds), так что делегат не позовётся.
        Func<int, IReadOnlyList<int>>? tagsProvider = _tagRepository is null
            ? null
            : trackId => _tagsByTrackId.TryGetValue(trackId, out var tags)
                ? tags.Select(t => t.Id).ToArray() : Array.Empty<int>();

        var filtered = LibraryFilter.Apply(_allTracks, criteria, tagsProvider);

        Track? selection = SelectedTrack;
        DisplayedTracks.Clear();
        foreach (var track in filtered)
        {
            DisplayedTracks.Add(track);
        }
        // WPF может сбросить TwoWay SelectedItem во время Clear; редактор сохраняет выбранный трек.
        SelectedTrack = selection is null ? null : _allTracks.FirstOrDefault(t => t.Id == selection.Id);

        // Computed-агрегаты пересчитываются вместе с DisplayedTracks. На 50k треков ~50ms.
        var filteredSnapshot = DisplayedTracks.ToList();
        DisplayedAlbums = LibraryGroupingService.GroupByAlbum(filteredSnapshot);
        DisplayedArtists = LibraryGroupingService.GroupByArtist(filteredSnapshot);

        // Спека «Фильтр-семантика»: drill-down обязан уважать активные фильтры. Если пользователь
        // сейчас смотрит детальную страницу альбома/исполнителя, переустанавливаем CurrentLeftColumn
        // на свежий агрегат из только что пересчитанных DisplayedAlbums/DisplayedArtists — иначе
        // деталь продолжит показывать треклист, снятый ДО смены фильтра. Root-состояния и back-стек
        // не трогаем (ре-резолвим только видимую CurrentLeftColumn).
        OnPropertyChanged(nameof(TagsByTrackId));
        CurrentLeftColumn = RefreshDetail(_currentLeftColumn);
    }

    private LeftColumnState RefreshDetail(LeftColumnState state) => state switch
        {
            LeftColumnState.AlbumDetail albumDetail => new LeftColumnState.AlbumDetail(
                DisplayedAlbums.FirstOrDefault(a => a.AlbumKey == albumDetail.Album.AlbumKey)
                    ?? EmptyAlbum(albumDetail.Album)),
            LeftColumnState.ArtistDetail artistDetail => new LeftColumnState.ArtistDetail(
                DisplayedArtists.FirstOrDefault(a => a.Name == artistDetail.Artist.Name)
                    ?? EmptyArtist(artistDetail.Artist)),
            LeftColumnState.PlaylistDetail playlistDetail when _playlists is not null =>
                _playlists.Playlists.Any(p => p.Id == playlistDetail.Playlist.Playlist.Id)
                    ? new LeftColumnState.PlaylistDetail(_playlists.BuildView(playlistDetail.Playlist.Playlist.Id))
                    : new LeftColumnState.PlaylistsRoot(),
            _ => state
        };

    /// <summary>
    /// Строит «пустой» агрегат альбома, сохраняя identity (Title/Artist/Year/Cover) от последнего
    /// известного состояния, но с пустым треклистом. Используется, когда активный фильтр убрал из
    /// альбома все треки и GroupByAlbum его больше не возвращает — деталь-страница должна показать
    /// header + empty-state, а не застрявший до-фильтра треклист.
    /// </summary>
    private static AlbumAggregate EmptyAlbum(AlbumAggregate previous) => previous with
    {
        Tracks = Array.Empty<Track>()
    };

    /// <summary>Аналог EmptyAlbum для исполнителя: сохраняет Name, обнуляет Albums/LooseTracks/счётчики.</summary>
    private static ArtistAggregate EmptyArtist(ArtistAggregate previous) => previous with
    {
        Albums = Array.Empty<AlbumAggregate>(),
        LooseTracks = Array.Empty<Track>(),
        TotalTracks = 0,
        TotalDuration = TimeSpan.Zero
    };

    private void SkipBy(TimeSpan delta)
    {
        TimeSpan target = _audioPlayerService.Position + delta;
        TimeSpan duration = _audioPlayerService.Duration;
        if (target < TimeSpan.Zero)
        {
            target = TimeSpan.Zero;
        }
        else if (duration > TimeSpan.Zero && target > duration)
        {
            target = duration;
        }

        _audioPlayerService.Position = target;
        CurrentPosition = target;
    }

    private bool CanGoToOffset(int offset)
    {
        if (PlayingTrack is null || _playbackQueue.Count == 0)
        {
            return false;
        }

        int index = IndexOfPlayingTrack();
        if (index < 0)
        {
            return false;
        }

        int target = index + offset;
        return target >= 0 && target < _playbackQueue.Count;
    }

    private void GoToTrackByOffset(int offset)
    {
        if (PlayingTrack is null)
        {
            return;
        }

        int index = IndexOfPlayingTrack();
        if (index < 0)
        {
            return;
        }

        int target = index + offset;
        if (target < 0 || target >= _playbackQueue.Count)
        {
            return;
        }

        Track next = _playbackQueue[target];
        if (!EnsureTrackExists(next)) return;
        _queueIndex = target;
        SelectedTrack = next;
        StartOrResumeTrack(next, forceRestart: true);
    }

    // Ищет играющий трек В ОЧЕРЕДИ ВОСПРОИЗВЕДЕНИЯ (_playbackQueue), а не в DisplayedTracks —
    // иначе next/prev после Play-альбома ходили бы по вкладке «Треки», а не по треклисту альбома.
    private int IndexOfPlayingTrack()
    {
        if (PlayingTrack is null)
        {
            return -1;
        }

        return _queueIndex >= 0 && _queueIndex < _playbackQueue.Count
            && _playbackQueue[_queueIndex].Id == PlayingTrack.Id ? _queueIndex : -1;
    }

    private void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.Current,
            RepeatMode.Current => RepeatMode.Library,
            _ => RepeatMode.Off
        };
    }

    private void PlayOrPause(Track? requested = null)
    {
        Track? target = requested ?? SelectedTrack ?? PlayingTrack;
        if (target is null)
        {
            SetStatus(OperationResult.Error("Выберите трек."));
            return;
        }

        if (PlayingTrack?.Id == target.Id && IsPlaying)
        {
            _audioPlayerService.Pause();
            _progressTimer.Stop();
            IsPlaying = false;
            _isPaused = true;
            SetStatus(OperationResult.Info("Воспроизведение приостановлено."));
            return;
        }

        // Воспроизведение, начатое из вкладки «Треки» (Play/Pause на выбранном треке), ходит по
        // текущей отфильтрованной библиотеке — снимаем её снимок в очередь (bug #5 fix).
        if (!EnsureTrackExists(target)) return;
        if (!_isPaused || PlayingTrack?.Id != target.Id)
        {
            _playbackQueue = DisplayedTracks.ToList();
            _queueIndex = _playbackQueue.FindIndex(t => t.Id == target.Id);
        }
        StartOrResumeTrack(target);
    }

    /// <summary>
    /// Заполняет _playbackQueue треками альбома/исполнителя/detail-выбора и запускает воспроизведение.
    /// НЕ трогает DisplayedTracks — вкладка «Треки» остаётся отфильтрованной библиотекой (bug #5).
    /// </summary>
    /// <param name="selectedTrack">
    /// Трек, с которого нужно начать (клик по конкретной строке в detail-списке). Если null — играем
    /// с начала очереди (после шаффла — с получившегося первого элемента), как раньше делал PlayAlbum/PlayArtist.
    /// </param>
    private void ReplaceQueueAndPlay(System.Collections.Generic.IReadOnlyList<Track> tracks, bool shuffle, Track? selectedTrack = null)
    {
        var queue = tracks.ToList();
        if (shuffle)
        {
            // Fisher-Yates на копии.
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = _shuffleRng.Next(i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
        }

        // Сначала сравниваем экземпляр: в плейлисте один Id может встречаться несколько раз.
        if (queue.Count == 0) return;
        int startIndex = selectedTrack is null ? 0 : queue.FindIndex(t => ReferenceEquals(t, selectedTrack));
        if (startIndex < 0) startIndex = queue.FindIndex(t => t.Id == selectedTrack!.Id);
        if (startIndex < 0) startIndex = 0;
        Track start = queue[startIndex];
        if (!EnsureTrackExists(start)) return;
        _playbackQueue = queue;
        _queueIndex = startIndex;

        SelectedTrack = start;
        // Не через PlayPauseCommand/PlayOrPause: тот безусловно переснимает _playbackQueue из
        // DisplayedTracks (нормальный сценарий воспроизведения из вкладки «Треки»), что затёрло бы
        // только что собранную очередь альбома/исполнителя. Здесь очередь уже собрана — просто играем.
        StartOrResumeTrack(start, forceRestart: true);
    }

    private void PlaySpecificTrack(Track? track)
    {
        if (track is null)
        {
            return;
        }

        SelectedTrack = track;
        if (PlayingTrack is not null && PlayingTrack.Id == track.Id && IsPlaying)
        {
            SetStatus(OperationResult.Info("Этот трек уже воспроизводится."));
            return;
        }

        // Двойной клик по треку в вкладке «Треки» (или replay истории) — очередь = снимок
        // текущей отфильтрованной библиотеки, чтобы next/prev продолжили по ней (bug #5 fix).
        if (!EnsureTrackExists(track)) return;
        if (!_isPaused || PlayingTrack?.Id != track.Id)
        {
            _playbackQueue = DisplayedTracks.ToList();
            _queueIndex = _playbackQueue.FindIndex(t => t.Id == track.Id);
        }
        StartOrResumeTrack(track);
    }

    private void ReplayHistoryEntry(PlaybackEntry? entry)
    {
        if (entry?.Track is null)
        {
            return;
        }

        PlaySpecificTrack(entry.Track);
    }

    private bool EnsureTrackExists(Track track)
    {
        if (_fileService.Exists(track.FilePath)) return true;
        SetStatus(OperationResult.Error($"Файл не найден: {track.FilePath}"));
        return false;
    }

    private void StartOrResumeTrack(Track track, bool forceRestart = false)
    {
        // Every playback entry point goes through this method so the Play button,
        // library double-click, and history replay keep identical pause/history rules.
        if (!_fileService.Exists(track.FilePath))
        {
            SetStatus(OperationResult.Error($"Файл не найден: {track.FilePath}"));
            return;
        }

        bool isResume = !forceRestart && _isPaused && PlayingTrack is not null && PlayingTrack.Id == track.Id;

        if (!isResume)
        {
            if (PlayingTrack is not null)
            {
                ResetPlaybackState();
            }

            PlayingTrack = track;
            _pendingHistoryTrack = track;

            OperationResult openResult = _audioPlayerService.Open(track.FilePath);
            if (!openResult.IsSuccess)
            {
                ResetPlaybackState();
                SetStatus(openResult);
                return;
            }
        }

        OperationResult playResult = _audioPlayerService.Play();
        SetStatus(playResult);

        if (playResult.IsSuccess)
        {
            IsPlaying = true;
            _isPaused = false;
            _progressTimer.Start();
            return;
        }

        if (!isResume)
        {
            ResetPlaybackState();
        }
    }

    private void Stop()
    {
        ResetPlaybackState();
        SetStatus(OperationResult.Info("Воспроизведение остановлено."));
    }

    private void ResetPlaybackState()
    {
        _audioPlayerService.Stop();
        _progressTimer.Stop();
        IsPlaying = false;
        _isPaused = false;
        PlayingTrack = null;
        _pendingHistoryTrack = null;
        CurrentPosition = TimeSpan.Zero;
        CurrentDuration = TimeSpan.Zero;
    }

    private void SaveSelectedTrack()
    {
        if (SelectedTrack is null)
        {
            SetStatus(OperationResult.Error("Выберите трек для сохранения."));
            return;
        }

        if (!_fileService.Exists(SelectedTrack.FilePath))
        {
            SetStatus(OperationResult.Error($"Файл не найден: {SelectedTrack.FilePath}"));
            return;
        }

        string suggestedName = _fileService.GetFileName(SelectedTrack.FilePath);
        string? targetPath = _saveFileDialogService.PickSavePath(suggestedName);
        if (targetPath is null)
        {
            SetStatus(OperationResult.Info("Сохранение отменено."));
            return;
        }

        SetStatus(_fileService.Copy(SelectedTrack.FilePath, targetPath, overwrite: true));
    }

    private void AddToHistory(Track track)
    {
        var entry = new PlaybackEntry { Track = track, PlayedAt = DateTime.Now };
        try { _listeningHistoryRepository.Append(entry); }
        catch (Exception ex) when (MusicLibrary.Services.StorageErrors.IsExpected(ex))
        {
            ReportStorageError("Воспроизведение продолжается, но историю сохранить не удалось", ex);
            return;
        }

        PlaybackHistory.Insert(0, entry);
        while (PlaybackHistory.Count > MaxHistoryItems)
        {
            PlaybackHistory.RemoveAt(PlaybackHistory.Count - 1);
        }
    }

    private void RefreshDuration()
    {
        CurrentDuration = _audioPlayerService.Duration;
    }

    private void HandleMediaOpened(string filePath)
    {
        if (PlayingTrack is null || !IsSamePath(PlayingTrack.FilePath, filePath))
        {
            return;
        }

        RefreshDuration();

        if (_pendingHistoryTrack is not null && IsSamePath(_pendingHistoryTrack.FilePath, filePath))
        {
            AddToHistory(_pendingHistoryTrack);
            _pendingHistoryTrack = null;
        }
    }

    private static bool IsSamePath(string expectedPath, string actualPath)
    {
        // MediaOpened приходит асинхронно, поэтому сверяем путь и отбрасываем устаревшие события.
        return string.Equals(
            Path.GetFullPath(expectedPath),
            Path.GetFullPath(actualPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private void SeekTo(TimeSpan? target)
    {
        if (target is null || PlayingTrack is null)
        {
            return;
        }

        TimeSpan duration = _audioPlayerService.Duration;
        TimeSpan clamped = target.Value;
        if (clamped < TimeSpan.Zero)
        {
            clamped = TimeSpan.Zero;
        }
        else if (duration > TimeSpan.Zero && clamped > duration)
        {
            clamped = duration;
        }

        _audioPlayerService.Position = clamped;
        CurrentPosition = clamped;
    }

    private void RefreshProgress()
    {
        // Пока пользователь тянет ползунок seek-слайдера, не перезаписываем его значение из плеера —
        // иначе позиция будет дёргаться обратно на каждом тике таймера.
        if (_isSeeking)
        {
            return;
        }

        CurrentPosition = _audioPlayerService.Position;
    }

    private void HandleMediaEnded()
    {
        Track? finished = PlayingTrack;

        _audioPlayerService.Stop();
        _progressTimer.Stop();
        IsPlaying = false;
        _isPaused = false;
        CurrentPosition = TimeSpan.Zero;
        CurrentDuration = TimeSpan.Zero;
        _pendingHistoryTrack = null;

        if (finished is null)
        {
            return;
        }

        // Авто-переход по завершении трека ходит по очереди воспроизведения (_playbackQueue),
        // не по DisplayedTracks — иначе после Play-альбома auto-next перескочил бы в вкладку «Треки».
        int nextIndex = RepeatMode == RepeatMode.Current ? _queueIndex : _queueIndex + 1;
        if (RepeatMode == RepeatMode.Library && nextIndex >= _playbackQueue.Count) nextIndex = 0;
        Track? next = RepeatMode == RepeatMode.Current ? finished
            : _queueIndex >= 0 && nextIndex >= 0 && nextIndex < _playbackQueue.Count
                ? _playbackQueue[nextIndex] : null;
        if (next is null)
        {
            PlayingTrack = null;
            SetStatus(OperationResult.Info("Воспроизведение завершено."));
            return;
        }

        PlayingTrack = null;
        _queueIndex = nextIndex;
        SelectedTrack = next;
        StartOrResumeTrack(next);
    }

    // MediaFailed обрывает цепочку auto-next: иначе на повреждённой библиотеке можно уйти в каскад ошибок.
    private void HandleMediaFailed(string message)
    {
        ResetPlaybackState();
        SetStatus(OperationResult.Error($"Ошибка воспроизведения: {message}"));
    }

    private void SetStatus(OperationResult result)
    {
        StatusMessage = result.Message;
        StatusKind = result.Kind;
    }

    private void PersistSettings()
    {
        try { _playerSettingsRepository.Save(new PlayerSettings(_volume, _isMuted, _repeatMode)); }
        catch (Exception ex) when (MusicLibrary.Services.StorageErrors.IsExpected(ex))
        {
            ReportStorageError("Не удалось сохранить настройки", ex);
        }
    }

    private void ReportStorageError(string operation, Exception exception)
    {
        System.Diagnostics.Trace.TraceError("{0}: {1}", operation, exception);
        SetStatus(OperationResult.Error($"{operation}: {exception.Message}"));
    }

    private RelayCommand CreateCommand(Action<object?> execute, Predicate<object?>? canExecute = null) => new(
        p =>
        {
            try { execute(p); }
            catch (Exception ex) when (MusicLibrary.Services.StorageErrors.IsExpected(ex))
            {
                ReportStorageError("Не удалось выполнить операцию с библиотекой", ex);
            }
        }, canExecute);

    /// <summary>
    /// Обработчик PlaylistsViewModel.PlaylistContentChanged: если сейчас открыта деталь именно
    /// этого плейлиста — пересобрать её свежими треками (add/remove/reorder уже применены в репозитории).
    /// </summary>
    private void OnPlaylistContentChanged(int playlistId)
    {
        if (_currentLeftColumn is LeftColumnState.PlaylistDetail d && d.Playlist.Playlist.Id == playlistId)
            CurrentLeftColumn = RefreshDetail(d);
    }

    private void OnPlaylistsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaylistsViewModel.ErrorMessage) && _playlists?.ErrorMessage is { } error)
            SetStatus(OperationResult.Error(error));
    }

    private void OnMediaOpened(object? sender, string path) => HandleMediaOpened(path);
    private void OnMediaEnded(object? sender, EventArgs e) => HandleMediaEnded();
    private void OnMediaFailed(object? sender, string message) => HandleMediaFailed(message);
    private void OnProgressTick(object? sender, EventArgs e) => RefreshProgress();

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _audioPlayerService.MediaOpened -= OnMediaOpened;
        _audioPlayerService.MediaEnded -= OnMediaEnded;
        _audioPlayerService.MediaFailed -= OnMediaFailed;
        if (_playlists is not null)
        {
            _playlists.PlaylistContentChanged -= OnPlaylistContentChanged;
            _playlists.PropertyChanged -= OnPlaylistsPropertyChanged;
        }
        _progressTimer.Stop();
        _progressTimer.Tick -= OnProgressTick;
        _audioPlayerService.Dispose();
    }
}
