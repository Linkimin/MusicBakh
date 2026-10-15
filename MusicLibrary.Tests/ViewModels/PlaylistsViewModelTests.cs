using System.Collections.Generic;
using System.Linq;
using MusicBakh.Application.Abstractions;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;
using Xunit;

namespace MusicLibrary.Tests.ViewModels;

public sealed class PlaylistsViewModelTests
{
    private sealed class FakeClock : IClock { public System.DateTime UtcNow { get; set; } = new(2026,1,1,0,0,0,System.DateTimeKind.Utc); }
    private sealed class YesConfirm : IConfirmationService { public bool Confirm(string t, string m) => true; }

    // Минимальный in-memory IPlaylistRepository для VM-тестов.
    private sealed class FakePlaylistRepo : IPlaylistRepository
    {
        private readonly List<Playlist> _pl = new();
        private readonly Dictionary<int, List<int>> _tracks = new();
        private int _next = 1;
        public Exception? CreateError { get; set; }
        public IReadOnlyList<Playlist> GetAll() => _pl.OrderByDescending(p => p.Id).ToList();
        public Playlist? GetById(int id) => _pl.FirstOrDefault(p => p.Id == id);
        public Playlist Create(string name) { if (CreateError is not null) throw CreateError; var p = new Playlist(_next++, name, System.DateTime.UtcNow); _pl.Add(p); _tracks[p.Id] = new(); return p; }
        public void Rename(int id, string name) { var i = _pl.FindIndex(p => p.Id == id); if (i>=0) _pl[i] = _pl[i] with { Name = name }; }
        public void Delete(int id) { _pl.RemoveAll(p => p.Id == id); _tracks.Remove(id); }
        public IReadOnlyList<Track> GetTracks(int id) => _tracks[id].Select(tid => new Track { Id = tid, Title = "t"+tid, FilePath = tid+".mp3" }).ToList();
        public int GetTrackCount(int id) => _tracks[id].Count;
        public void AddTrack(int id, int tid) => _tracks[id].Add(tid);
        public void AddTracks(int id, IReadOnlyList<int> ids) => _tracks[id].AddRange(ids);
        public void RemoveAt(int id, int pos) => _tracks[id].RemoveAt(pos);
        public void Reorder(int id, int from, int to) { var l=_tracks[id]; var x=l[from]; l.RemoveAt(from); l.Insert(to,x); }
    }

    private static PlaylistsViewModel Vm(FakePlaylistRepo repo) => new(repo, new YesConfirm(), new FakeClock());

    [Fact]
    public void StorageFailure_KeepsInputOpenAndReportsError()
    {
        var repo = new FakePlaylistRepo { CreateError = new System.IO.IOException("disk unavailable") };
        var vm = Vm(repo);
        vm.IsCreating = true;
        vm.ConfirmNewPlaylistName("Mix");
        Assert.True(vm.IsCreating);
        Assert.Empty(vm.Playlists);
        Assert.Contains("disk unavailable", vm.ErrorMessage);
    }

    [Fact]
    public void CreatePlaylist_Adds_To_List()
    {
        var repo = new FakePlaylistRepo();
        var vm = Vm(repo);
        vm.ConfirmNewPlaylistName("Мой микс");
        Assert.Contains(vm.Playlists, p => p.Name == "Мой микс");
    }

    [Fact]
    public void Empty_Name_Falls_Back_To_Default()
    {
        var repo = new FakePlaylistRepo();
        var vm = Vm(repo);
        vm.ConfirmNewPlaylistName("   ");
        Assert.Contains(vm.Playlists, p => p.Name == "Новый плейлист");
    }

    [Fact]
    public void AddContextTarget_Adds_Captured_Tracks_To_Playlist()
    {
        var repo = new FakePlaylistRepo();
        var vm = Vm(repo);
        var p = repo.Create("P"); vm.RefreshPlaylists();
        vm.SetContextTarget(new[] { new Track { Id = 7, FilePath = "7.mp3" }, new Track { Id = 8, FilePath = "8.mp3" } });
        vm.AddContextTargetToPlaylist(p);
        Assert.Equal(new[] { 7, 8 }, repo.GetTracks(p.Id).Select(t => t.Id).ToArray());
    }
}
