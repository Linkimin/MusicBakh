using MusicLibrary.Commands;
using MusicLibrary.Models;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Playback;
using MusicLibrary.Services.Storage;
using MusicLibrary.Services.Tracks;
using MusicLibrary.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace MusicLibrary.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private const string AllGenres = "Все жанры";
    private const int MaxHistoryItems = 50;

    private readonly List<Track> _allTracks;
    private readonly IFileService _fileService;
    private readonly ISaveFileDialogService _saveFileDialogService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IAddTrackDialogService? _addTrackDialogService;
    private readonly IUserTrackStorage? _userTrackStorage;
    private readonly IConfirmationService? _confirmationService;
    private readonly IPlayerSettingsStorage? _playerSettingsStorage;
    private readonly DispatcherTimer _progressTimer;

    private string _selectedGenre = AllGenres;
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

    public MainViewModel(
        ITrackRepository trackRepository,
        IFileService fileService,
        ISaveFileDialogService saveFileDialogService,
        IAudioPlayerService audioPlayerService,
        IAddTrackDialogService? addTrackDialogService = null,
        IUserTrackStorage? userTrackStorage = null,
        IConfirmationService? confirmationService = null,
        IPlayerSettingsStorage? playerSettingsStorage = null)
    {
        _fileService = fileService;
        _saveFileDialogService = saveFileDialogService;
        _audioPlayerService = audioPlayerService;
        _addTrackDialogService = addTrackDialogService;
        _userTrackStorage = userTrackStorage;
        _confirmationService = confirmationService;
        _playerSettingsStorage = playerSettingsStorage;

        _allTracks = new List<Track>(trackRepository.GetTracks());
        DisplayedTracks = new ObservableCollection<Track>(_allTracks);
        PlaybackHistory = new ObservableCollection<PlaybackEntry>();
        Genres = new ObservableCollection<string>(
            new[] { AllGenres }
                .Concat(_allTracks.Select(track => track.Genre).Distinct().OrderBy(genre => genre)));

        PlayPauseCommand = new RelayCommand(_ => PlayOrPause(), _ => SelectedTrack is not null);
        StopCommand = new RelayCommand(_ => Stop(), _ => PlayingTrack is not null);
        SaveTrackCommand = new RelayCommand(_ => SaveSelectedTrack(), _ => SelectedTrack is not null);
        AddTrackCommand = new RelayCommand(_ => OpenAddTrackDialog(), _ => _addTrackDialogService is not null && _userTrackStorage is not null);
        DeleteTrackCommand = new RelayCommand(_ => DeleteSelectedTrack(), _ => CanDeleteSelected);
        PlayTrackCommand = new RelayCommand(
            parameter => PlaySpecificTrack(parameter as Track),
            parameter => parameter is Track);
        ReplayHistoryEntryCommand = new RelayCommand(
            parameter => ReplayHistoryEntry(parameter as PlaybackEntry),
            parameter => parameter is PlaybackEntry);

        SkipForwardCommand = new RelayCommand(_ => SkipBy(TimeSpan.FromSeconds(10)), _ => PlayingTrack is not null);
        SkipBackwardCommand = new RelayCommand(_ => SkipBy(TimeSpan.FromSeconds(-10)), _ => PlayingTrack is not null);
        PreviousTrackCommand = new RelayCommand(_ => GoToTrackByOffset(-1), _ => CanGoToOffset(-1));
        NextTrackCommand = new RelayCommand(_ => GoToTrackByOffset(+1), _ => CanGoToOffset(+1));
        ToggleMuteCommand = new RelayCommand(_ => IsMuted = !IsMuted);
        CycleRepeatModeCommand = new RelayCommand(_ => CycleRepeatMode());

        _audioPlayerService.MediaOpened += (_, filePath) => HandleMediaOpened(filePath);
        _audioPlayerService.MediaEnded += (_, _) => HandleMediaEnded();
        _audioPlayerService.MediaFailed += (_, message) => HandleMediaFailed(message);

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _progressTimer.Tick += (_, _) => RefreshProgress();
    }

    public ObservableCollection<Track> DisplayedTracks { get; }
    public ObservableCollection<string> Genres { get; }
    public ObservableCollection<PlaybackEntry> PlaybackHistory { get; }

    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveTrackCommand { get; }
    public ICommand AddTrackCommand { get; }
    public ICommand DeleteTrackCommand { get; }
    public ICommand PlayTrackCommand { get; }
    public ICommand ReplayHistoryEntryCommand { get; }
    public ICommand SkipForwardCommand { get; }
    public ICommand SkipBackwardCommand { get; }
    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand CycleRepeatModeCommand { get; }

    public string SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            if (SetProperty(ref _selectedGenre, value))
            {
                ApplyGenreFilter();
            }
        }
    }

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
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasSelectedTrack => SelectedTrack is not null;

    public bool CanDeleteSelected =>
        SelectedTrack is not null
        && _userTrackStorage is not null
        && IsUserTrack(SelectedTrack);

    private bool IsUserTrack(Track track)
    {
        if (_userTrackStorage is null || string.IsNullOrEmpty(track.FilePath))
        {
            return false;
        }
        return track.FilePath.StartsWith(_userTrackStorage.MusicDirectory, StringComparison.OrdinalIgnoreCase);
    }

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

    public string CurrentPositionText => CurrentPosition.ToString(@"m\:ss");
    public string CurrentDurationText => CurrentDuration == TimeSpan.Zero ? "0:00" : CurrentDuration.ToString(@"m\:ss");
    public double ProgressValue => CurrentPosition.TotalSeconds;
    public double ProgressMaximum => Math.Max(CurrentDuration.TotalSeconds, 1);

    public int GetNextTrackId()
    {
        return _allTracks.Count == 0 ? 1 : _allTracks.Max(track => track.Id) + 1;
    }

    private void OpenAddTrackDialog()
    {
        if (_addTrackDialogService is null || _userTrackStorage is null)
        {
            return;
        }

        TrackImportCandidate? candidate = _addTrackDialogService.Show();
        if (candidate is null)
        {
            return;
        }

        var track = new Track
        {
            Id = GetNextTrackId(),
            Title = candidate.Title,
            Artist = candidate.Artist,
            Genre = candidate.Genre,
            Duration = candidate.Duration,
            FilePath = candidate.AudioFilePath,
            CoverPath = candidate.CoverFilePath
        };

        _userTrackStorage.Append(UserTrack.FromTrack(track, DateTime.Now));
        AddTrack(track);
        SetStatus(OperationResult.Success($"Трек «{track.Title}» добавлен в библиотеку."));
    }

    private void DeleteSelectedTrack()
    {
        Track? track = SelectedTrack;
        if (track is null || _userTrackStorage is null || !IsUserTrack(track))
        {
            return;
        }

        if (_confirmationService is not null)
        {
            bool confirmed = _confirmationService.Confirm(
                "Удалить трек",
                $"Удалить «{track.Artist} — {track.Title}» из библиотеки? Файл и обложка будут удалены безвозвратно.");
            if (!confirmed)
            {
                return;
            }
        }

        if (PlayingTrack is not null && PlayingTrack.Id == track.Id)
        {
            ResetPlaybackState();
        }

        _userTrackStorage.Delete(track.Id);
        RemoveTrack(track);
        SetStatus(OperationResult.Success($"Трек «{track.Title}» удален."));
    }

    private void RemoveTrack(Track track)
    {
        _allTracks.Remove(track);
        DisplayedTracks.Remove(track);

        // Если последний трек этого жанра ушёл — убираем жанр из фильтра.
        if (!_allTracks.Any(t => t.Genre == track.Genre) && Genres.Contains(track.Genre))
        {
            Genres.Remove(track.Genre);
            if (SelectedGenre == track.Genre)
            {
                SelectedGenre = AllGenres;
            }
        }

        if (SelectedTrack == track)
        {
            SelectedTrack = null;
        }
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

        if (SelectedGenre == AllGenres || SelectedGenre == track.Genre)
        {
            DisplayedTracks.Add(track);
        }
    }

    private void ApplyGenreFilter()
    {
        DisplayedTracks.Clear();

        IEnumerable<Track> tracks = SelectedGenre == AllGenres
            ? _allTracks
            : _allTracks.Where(track => track.Genre == SelectedGenre);

        foreach (Track track in tracks)
        {
            DisplayedTracks.Add(track);
        }
    }

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
        if (PlayingTrack is null || DisplayedTracks.Count == 0)
        {
            return false;
        }

        int index = IndexOfPlayingTrack();
        if (index < 0)
        {
            return false;
        }

        int target = index + offset;
        return target >= 0 && target < DisplayedTracks.Count;
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
        if (target < 0 || target >= DisplayedTracks.Count)
        {
            return;
        }

        Track next = DisplayedTracks[target];
        SelectedTrack = next;
        StartOrResumeTrack(next);
    }

    private int IndexOfPlayingTrack()
    {
        if (PlayingTrack is null)
        {
            return -1;
        }

        for (int i = 0; i < DisplayedTracks.Count; i++)
        {
            if (DisplayedTracks[i].Id == PlayingTrack.Id)
            {
                return i;
            }
        }
        return -1;
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

    private void PlayOrPause()
    {
        if (SelectedTrack is null)
        {
            SetStatus(OperationResult.Error("Выберите трек."));
            return;
        }

        if (IsSelectedPlaying && IsPlaying)
        {
            _audioPlayerService.Pause();
            _progressTimer.Stop();
            IsPlaying = false;
            _isPaused = true;
            SetStatus(OperationResult.Info("Воспроизведение приостановлено."));
            return;
        }

        StartOrResumeTrack(SelectedTrack);
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

    private void StartOrResumeTrack(Track track)
    {
        // Every playback entry point goes through this method so the Play button,
        // library double-click, and history replay keep identical pause/history rules.
        if (!_fileService.Exists(track.FilePath))
        {
            SetStatus(OperationResult.Error($"Файл не найден: {track.FilePath}"));
            return;
        }

        bool isResume = _isPaused && PlayingTrack is not null && PlayingTrack.Id == track.Id;

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
        PlaybackHistory.Insert(0, new PlaybackEntry { Track = track, PlayedAt = DateTime.Now });

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

    private void RefreshProgress()
    {
        CurrentPosition = _audioPlayerService.Position;
    }

    private void HandleMediaEnded()
    {
        ResetPlaybackState();
        SetStatus(OperationResult.Info("Воспроизведение завершено."));
    }

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
        _playerSettingsStorage?.Save(new PlayerSettings(_volume, _isMuted, _repeatMode));
    }

    public void Dispose()
    {
        _progressTimer.Stop();
        _audioPlayerService.Dispose();
    }
}
