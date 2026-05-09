using MusicLibrary.Commands;
using MusicLibrary.Models;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Import;
using MusicLibrary.ViewModels;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MusicLibrary.ViewModels;

public enum AddTrackMode
{
    LocalFile,
    Url
}

/// <summary>
/// ViewModel модального окна «Добавить трек». Управляет двумя режимами — выбор
/// файла или ввод URL — и поддерживает три фазы: подготовка → импорт с прогрессом
/// → редактирование найденных метаданных перед сохранением.
/// </summary>
public sealed class AddTrackViewModel : ViewModelBase
{
    private readonly IOpenFileDialogService _openFileDialog;
    private readonly ITrackImporter _importer;

    private AddTrackMode _mode = AddTrackMode.LocalFile;
    private string _localFilePath = string.Empty;
    private string _urlInput = string.Empty;
    private string _statusMessage = "Выберите файл или вставьте ссылку.";
    private OperationMessageKind _statusKind = OperationMessageKind.Info;
    private bool _isImporting;
    private double _progressValue;
    private TrackImportCandidate? _candidate;
    private BitmapImage? _coverPreview;
    private string _editTitle = string.Empty;
    private string _editArtist = string.Empty;
    private string _editGenre = string.Empty;

    public AddTrackViewModel(IOpenFileDialogService openFileDialog, ITrackImporter importer)
    {
        _openFileDialog = openFileDialog;
        _importer = importer;

        BrowseFileCommand = new RelayCommand(_ => BrowseFile(), _ => Mode == AddTrackMode.LocalFile && !IsImporting);
        StartImportCommand = new RelayCommand(_ => _ = StartImportAsync(), _ => CanStartImport());
        ConfirmCommand = new RelayCommand(_ => Confirm(), _ => HasCandidate && !IsImporting);
        CancelCommand = new RelayCommand(_ => RequestClose(false));
        SwitchToFileCommand = new RelayCommand(_ => Mode = AddTrackMode.LocalFile, _ => !IsImporting);
        SwitchToUrlCommand = new RelayCommand(_ => Mode = AddTrackMode.Url, _ => !IsImporting);
    }

    public event EventHandler<bool>? CloseRequested;

    public ICommand BrowseFileCommand { get; }
    public ICommand StartImportCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SwitchToFileCommand { get; }
    public ICommand SwitchToUrlCommand { get; }

    public AddTrackMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsLocalFileMode));
                OnPropertyChanged(nameof(IsUrlMode));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsLocalFileMode => Mode == AddTrackMode.LocalFile;
    public bool IsUrlMode => Mode == AddTrackMode.Url;

    public string LocalFilePath
    {
        get => _localFilePath;
        set
        {
            if (SetProperty(ref _localFilePath, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string UrlInput
    {
        get => _urlInput;
        set
        {
            if (SetProperty(ref _urlInput, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

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

    public bool IsImporting
    {
        get => _isImporting;
        private set
        {
            if (SetProperty(ref _isImporting, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool HasCandidate => _candidate is not null;

    public BitmapImage? CoverPreview
    {
        get => _coverPreview;
        private set => SetProperty(ref _coverPreview, value);
    }

    public string EditTitle
    {
        get => _editTitle;
        set => SetProperty(ref _editTitle, value);
    }

    public string EditArtist
    {
        get => _editArtist;
        set => SetProperty(ref _editArtist, value);
    }

    public string EditGenre
    {
        get => _editGenre;
        set => SetProperty(ref _editGenre, value);
    }

    public TrackImportCandidate? Result { get; private set; }

    private void BrowseFile()
    {
        string? path = _openFileDialog.PickAudioFile();
        if (!string.IsNullOrEmpty(path))
        {
            LocalFilePath = path;
            SetStatus(OperationResult.Info("Файл выбран. Нажмите «Импортировать»."));
        }
    }

    private bool CanStartImport()
    {
        if (IsImporting)
        {
            return false;
        }
        if (Mode == AddTrackMode.LocalFile)
        {
            return !string.IsNullOrWhiteSpace(LocalFilePath);
        }
        return !string.IsNullOrWhiteSpace(UrlInput);
    }

    private async Task StartImportAsync()
    {
        IsImporting = true;
        ProgressValue = 0;
        SetStatus(OperationResult.Info("Импортирую трек..."));

        try
        {
            var progress = new Progress<double>(value => ProgressValue = Math.Clamp(value, 0, 1));
            ImportRequest request = Mode == AddTrackMode.LocalFile
                ? new LocalFileImportRequest(LocalFilePath)
                : new UrlImportRequest(UrlInput.Trim());

            ImportResult result = await _importer.ImportAsync(request, progress, CancellationToken.None);

            if (!result.IsSuccess || result.Candidate is null)
            {
                SetStatus(OperationResult.Error(result.Message));
                return;
            }

            _candidate = result.Candidate;
            EditTitle = _candidate.Title;
            EditArtist = _candidate.Artist;
            EditGenre = _candidate.Genre;
            CoverPreview = LoadBitmap(_candidate.CoverFilePath);

            OnPropertyChanged(nameof(HasCandidate));
            SetStatus(OperationResult.Success("Метаданные найдены. Проверьте поля и сохраните."));
        }
        catch (Exception exception)
        {
            // Никакое исключение не должно оставлять окно в зависшем состоянии импорта.
            SetStatus(OperationResult.Error($"Ошибка импорта: {exception.Message}"));
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void Confirm()
    {
        if (_candidate is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            SetStatus(OperationResult.Error("Введите название трека."));
            return;
        }

        _candidate.Title = EditTitle.Trim();
        _candidate.Artist = string.IsNullOrWhiteSpace(EditArtist) ? "Неизвестный исполнитель" : EditArtist.Trim();
        _candidate.Genre = string.IsNullOrWhiteSpace(EditGenre) ? "Без жанра" : EditGenre.Trim();

        Result = _candidate;
        RequestClose(true);
    }

    private void RequestClose(bool confirmed)
    {
        CloseRequested?.Invoke(this, confirmed);
    }

    private void SetStatus(OperationResult result)
    {
        StatusMessage = result.Message;
        StatusKind = result.Kind;
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
