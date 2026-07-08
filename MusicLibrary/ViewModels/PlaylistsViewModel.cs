using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MusicBakh.Application.Abstractions;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.Commands;
using MusicLibrary.Services.Library;

namespace MusicLibrary.ViewModels;

/// <summary>
/// Под-VM плейлистов (первый шаг разгруза MainViewModel — ADR-0002). Владеет списком
/// плейлистов, инлайн-edit состоянием, CRUD и мутацией членства. Навигацию/воспроизведение
/// (общие для всех вкладок) выполняет MainViewModel.
/// </summary>
public sealed class PlaylistsViewModel : ViewModelBase
{
    private const string DefaultName = "Новый плейлист";

    private readonly IPlaylistRepository _repo;
    private readonly IConfirmationService _confirm;
    private readonly IClock _clock;

    private IReadOnlyList<Track> _contextTarget = System.Array.Empty<Track>();

    public PlaylistsViewModel(IPlaylistRepository repo, IConfirmationService confirm, IClock clock)
    {
        _repo = repo;
        _confirm = confirm;
        _clock = clock;
        Playlists = new ObservableCollection<Playlist>(_repo.GetAll());

        StartNewPlaylistCommand = new RelayCommand(_ => IsCreating = true);
        AddContextTargetToPlaylistCommand = new RelayCommand(p => { if (p is Playlist pl) AddContextTargetToPlaylist(pl); });
        DeletePlaylistCommand = new RelayCommand(p => { if (p is Playlist pl) DeletePlaylist(pl); });
        StartRenameCommand = new RelayCommand(p => { if (p is Playlist pl) EditingPlaylistId = pl.Id; });
        CancelRenameCommand = new RelayCommand(_ => EditingPlaylistId = null);
    }

    public ObservableCollection<Playlist> Playlists { get; }

    private bool _isCreating;
    public bool IsCreating { get => _isCreating; set => SetProperty(ref _isCreating, value); }

    /// <summary>
    /// Id плейлиста, чья строка сейчас переведена в инлайн-редактирование имени (null — ни одной).
    /// Минимальный аналог IsCreating для точечного per-row состояния: одновременно
    /// редактируется не более одной строки, конвертер в XAML сравнивает Id строки с этим значением.
    /// </summary>
    private int? _editingPlaylistId;
    public int? EditingPlaylistId { get => _editingPlaylistId; set => SetProperty(ref _editingPlaylistId, value); }

    public ICommand StartNewPlaylistCommand { get; }
    public ICommand AddContextTargetToPlaylistCommand { get; }
    public ICommand DeletePlaylistCommand { get; }
    public ICommand StartRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }

    public void RefreshPlaylists()
    {
        Playlists.Clear();
        foreach (var p in _repo.GetAll()) Playlists.Add(p);
    }

    public void ConfirmNewPlaylistName(string? name)
    {
        var final = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
        _repo.Create(final);
        IsCreating = false;
        RefreshPlaylists();
    }

    public void RenamePlaylist(Playlist p, string? name)
    {
        var final = string.IsNullOrWhiteSpace(name) ? p.Name : name.Trim();
        _repo.Rename(p.Id, final);
        EditingPlaylistId = null;
        RefreshPlaylists();
    }

    public void DeletePlaylist(Playlist p)
    {
        if (!_confirm.Confirm("Удалить плейлист", $"Удалить плейлист «{p.Name}»? Треки в библиотеке останутся."))
            return;
        _repo.Delete(p.Id);
        RefreshPlaylists();
    }

    public void SetContextTarget(IReadOnlyList<Track> tracks) => _contextTarget = tracks ?? System.Array.Empty<Track>();

    public void AddContextTargetToPlaylist(Playlist p)
    {
        if (_contextTarget.Count == 0) return;
        _repo.AddTracks(p.Id, _contextTarget.Select(t => t.Id).ToList());
        RefreshPlaylists();
        PlaylistContentChanged?.Invoke(p.Id);
    }

    public void AddTracksByIds(int playlistId, IReadOnlyList<int> ids)
    {
        if (ids.Count == 0) return;
        _repo.AddTracks(playlistId, ids);
        RefreshPlaylists();
        PlaylistContentChanged?.Invoke(playlistId);
    }

    public void RemoveTrackAt(int playlistId, int position)
    {
        _repo.RemoveAt(playlistId, position);
        RefreshPlaylists();
        PlaylistContentChanged?.Invoke(playlistId);
    }

    public void Reorder(int playlistId, int from, int to)
    {
        _repo.Reorder(playlistId, from, to);
        RefreshPlaylists();
        PlaylistContentChanged?.Invoke(playlistId);
    }

    public PlaylistView BuildView(int playlistId)
    {
        var pl = _repo.GetById(playlistId)!;
        return new PlaylistView(pl, _repo.GetTracks(playlistId));
    }

    /// <summary>
    /// Поднимается после любой мутации содержимого плейлиста (add/remove/reorder) с его Id.
    /// MainViewModel подписывается один раз в конструкторе и, если открыта деталь именно этого
    /// плейлиста, пересобирает её. Событие вместо мутабельного poll-флага — надёжнее: нельзя
    /// «забыть» вызвать ре-резолв в новой команде.
    /// </summary>
    public event System.Action<int>? PlaylistContentChanged;
}
