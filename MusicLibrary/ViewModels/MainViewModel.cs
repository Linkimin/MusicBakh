using MusicLibrary.Commands;
using MusicLibrary.Models;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Playback;
using MusicLibrary.Services.Tracks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace MusicLibrary.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private const string AllGenres = "Все жанры";
    private const int MaxHistoryItems = 50;

    private readonly IReadOnlyList<Track> _allTracks;
    private readonly IFileService _fileService;
    private readonly ISaveFileDialogService _saveFileDialogService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly DispatcherTimer _progressTimer;

    private string _selectedGenre = AllGenres;
    private Track? _selectedTrack;
    private string _statusMessage = "Выберите трек для воспроизведения.";
    private OperationMessageKind _statusKind = OperationMessageKind.Info;
    private bool _isPlaying;
    private bool _isPaused;
    private Track? _loadedTrack;
    private TimeSpan _currentPosition;
    private TimeSpan _currentDuration;

    public MainViewModel(
        ITrackRepository trackRepository,
        IFileService fileService,
        ISaveFileDialogService saveFileDialogService,
        IAudioPlayerService audioPlayerService)
    {
        _fileService = fileService;
        _saveFileDialogService = saveFileDialogService;
        _audioPlayerService = audioPlayerService;

        _allTracks = trackRepository.GetTracks();
        DisplayedTracks = new ObservableCollection<Track>(_allTracks);
        PlaybackHistory = new ObservableCollection<PlaybackEntry>();
        Genres = new ObservableCollection<string>(
            new[] { AllGenres }
                .Concat(_allTracks.Select(track => track.Genre).Distinct().OrderBy(genre => genre)));

        PlayPauseCommand = new RelayCommand(_ => PlayOrPause(), _ => SelectedTrack is not null);
        StopCommand = new RelayCommand(_ => Stop(), _ => SelectedTrack is not null);
        SaveTrackCommand = new RelayCommand(_ => SaveSelectedTrack(), _ => SelectedTrack is not null);

        _audioPlayerService.MediaOpened += (_, _) => RefreshDuration();
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
                Stop();
                OnPropertyChanged(nameof(HasSelectedTrack));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasSelectedTrack => SelectedTrack is not null;

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

    public string PlayPauseText => IsPlaying ? "Пауза" : "Воспроизвести";

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

    private void PlayOrPause()
    {
        if (SelectedTrack is null)
        {
            SetStatus(OperationResult.Error("Выберите трек."));
            return;
        }

        if (IsPlaying)
        {
            _audioPlayerService.Pause();
            _progressTimer.Stop();
            IsPlaying = false;
            _isPaused = true;
            SetStatus(OperationResult.Info("Воспроизведение приостановлено."));
            return;
        }

        if (!_fileService.Exists(SelectedTrack.FilePath))
        {
            SetStatus(OperationResult.Error($"Файл не найден: {SelectedTrack.FilePath}"));
            return;
        }

        bool isResume = _isPaused && _loadedTrack?.Id == SelectedTrack.Id;

        if (!isResume)
        {
            OperationResult openResult = _audioPlayerService.Open(SelectedTrack.FilePath);
            if (!openResult.IsSuccess)
            {
                SetStatus(openResult);
                return;
            }

            _loadedTrack = SelectedTrack;
        }

        OperationResult playResult = _audioPlayerService.Play();
        SetStatus(playResult);

        if (playResult.IsSuccess)
        {
            IsPlaying = true;
            _isPaused = false;
            _progressTimer.Start();

            // История фиксирует новый запуск трека, но не дублирует простое продолжение после паузы.
            if (!isResume)
            {
                AddToHistory(SelectedTrack);
            }
        }
    }

    private void Stop()
    {
        _audioPlayerService.Stop();
        _progressTimer.Stop();
        IsPlaying = false;
        _isPaused = false;
        _loadedTrack = null;
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

    private void RefreshProgress()
    {
        CurrentPosition = _audioPlayerService.Position;
    }

    private void HandleMediaEnded()
    {
        _progressTimer.Stop();
        IsPlaying = false;
        _isPaused = false;
        _loadedTrack = null;
        CurrentPosition = TimeSpan.Zero;
        CurrentDuration = TimeSpan.Zero;
        SetStatus(OperationResult.Info("Воспроизведение завершено."));
    }

    private void HandleMediaFailed(string message)
    {
        _progressTimer.Stop();
        IsPlaying = false;
        _isPaused = false;
        _loadedTrack = null;
        CurrentPosition = TimeSpan.Zero;
        CurrentDuration = TimeSpan.Zero;
        SetStatus(OperationResult.Error($"Ошибка воспроизведения: {message}"));
    }

    private void SetStatus(OperationResult result)
    {
        StatusMessage = result.Message;
        StatusKind = result.Kind;
    }

    public void Dispose()
    {
        _progressTimer.Stop();
        _audioPlayerService.Dispose();
    }
}
