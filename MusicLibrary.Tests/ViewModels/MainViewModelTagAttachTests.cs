using MusicBakh.Application.Abstractions;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;
using Xunit;
using System.IO;

namespace MusicLibrary.Tests.ViewModels;

public sealed class MainViewModelTagAttachTests
{
    [Fact]
    public void Detach_RefreshesActiveTagFilter()
    {
        var repo = new FakeTagRepo();
        var tag = repo.Add(new Tag { Id = 1, Name = "Morning" });
        repo.AttachTag(1, 1);
        using var vm = CreateVM(new[] { new Track { Id = 1 } }, repo);
        vm.ToggleTagFilterCommand.Execute(vm.TagFilters[0]);
        vm.SelectedTrack = vm.DisplayedTracks[0];
        vm.DetachTagFromSelectedCommand.Execute(tag);
        Assert.Empty(vm.DisplayedTracks);
    }

    [Fact]
    public void RefreshTags_RefreshesNamesInTrackCache()
    {
        var repo = new FakeTagRepo();
        repo.Add(new Tag { Id = 1, Name = "Old" });
        repo.AttachTag(1, 1);
        using var vm = CreateVM(new[] { new Track { Id = 1 } }, repo);
        repo.Tags[0] = new Tag { Id = 1, Name = "New" };
        vm.RefreshTagFiltersCommand.Execute(null);
        Assert.Equal("New", Assert.Single(vm.TagsByTrackId[1]).Name);
    }

    private sealed class FakeTagRepo : ITagRepository
    {
        public List<Tag> Tags { get; } = new();
        public Dictionary<int, List<int>> Attachments { get; } = new();
        public IReadOnlyList<Tag> GetAll() => Tags;
        public IReadOnlyDictionary<int, IReadOnlyList<Tag>> GetTagsByTrack() =>
            Attachments.ToDictionary(p => p.Key, p => GetTagsForTrack(p.Key));
        public Tag? FindById(int id) => Tags.FirstOrDefault(t => t.Id == id);
        public Tag Add(Tag tag) { Tags.Add(tag); return tag; }
        public void Update(Tag tag) { }
        public void Remove(int id) => Tags.RemoveAll(t => t.Id == id);
        public IReadOnlyList<Tag> GetTagsForTrack(int trackId) =>
            Attachments.TryGetValue(trackId, out var ids)
                ? ids.Select(i => Tags.First(t => t.Id == i)).ToList()
                : Array.Empty<Tag>();
        public void AttachTag(int trackId, int tagId)
        {
            if (!Attachments.ContainsKey(trackId)) Attachments[trackId] = new();
            if (!Attachments[trackId].Contains(tagId)) Attachments[trackId].Add(tagId);
        }
        public void DetachTag(int trackId, int tagId)
        {
            if (Attachments.TryGetValue(trackId, out var ids)) ids.Remove(tagId);
        }
    }

    [Fact]
    public void Attach_Adds_Tag_To_Cache_And_Repository()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var tagRepo = new FakeTagRepo();
        var morning = tagRepo.Add(new Tag { Id = 1, Name = "утро" });
        var vm = CreateVM(tracks, tagRepo);
        vm.SelectedTrack = vm.DisplayedTracks[0];

        vm.AttachTagToSelectedCommand.Execute(morning);

        Assert.Contains(morning, vm.TagsByTrackId[1]);
        Assert.Contains(1, tagRepo.Attachments[1]);
    }

    [Fact]
    public void Attach_Is_Idempotent_No_Duplicate_In_Cache()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var tagRepo = new FakeTagRepo();
        var t = tagRepo.Add(new Tag { Id = 1, Name = "утро" });
        var vm = CreateVM(tracks, tagRepo);
        vm.SelectedTrack = vm.DisplayedTracks[0];

        vm.AttachTagToSelectedCommand.Execute(t);
        vm.AttachTagToSelectedCommand.Execute(t);

        Assert.Single(vm.TagsByTrackId[1]);
    }

    [Fact]
    public void Detach_Removes_Tag_From_Cache_And_Repository()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var tagRepo = new FakeTagRepo();
        var t = tagRepo.Add(new Tag { Id = 1, Name = "утро" });
        tagRepo.AttachTag(1, 1);
        var vm = CreateVM(tracks, tagRepo);
        vm.SelectedTrack = vm.DisplayedTracks[0];

        Assert.Single(vm.TagsByTrackId[1]);

        vm.DetachTagFromSelectedCommand.Execute(t);

        Assert.Empty(vm.TagsByTrackId[1]);
        Assert.Empty(tagRepo.Attachments[1]);
    }

    [Fact]
    public void AvailableTagsForAttach_Excludes_Already_Attached()
    {
        var tracks = new[] { new Track { Id = 1, Title = "T", Artist = "A", FilePath = "1.mp3" } };
        var tagRepo = new FakeTagRepo();
        tagRepo.Add(new Tag { Id = 1, Name = "утро" });
        tagRepo.Add(new Tag { Id = 2, Name = "работа" });
        tagRepo.AttachTag(1, 1);
        var vm = CreateVM(tracks, tagRepo);
        vm.SelectedTrack = vm.DisplayedTracks[0];

        var available = vm.AvailableTagsForAttach.ToList();
        Assert.Single(available);
        Assert.Equal("работа", available[0].Name);
    }

    // === Фабрика VM и минимальные фейки (скопированы из MainViewModelTests, т.к. там private) ===

    private static MainViewModel CreateVM(IReadOnlyList<Track> tracks, ITagRepository tagRepo)
    {
        return new MainViewModel(
            new FakeTrackRepo(tracks),
            new FakeFileService(),
            new FakeSaveFileDialogService(),
            new FakeAudioPlayerService(),
            new FakeListeningHistoryRepository(),
            new FakePlayerSettingsRepository(),
            addTrackDialogService: null,
            confirmationService: null,
            searchService: null,
            statsWindowService: null,
            tagRepository: tagRepo);
    }

    private sealed class FakeTrackRepo : ITrackRepository
    {
        private readonly List<Track> _tracks;
        public FakeTrackRepo(IEnumerable<Track> seed) => _tracks = seed.ToList();
        public IReadOnlyList<Track> GetAll() => _tracks;
        public Track? FindById(int id) => _tracks.FirstOrDefault(t => t.Id == id);
        public Track Add(Track track) { _tracks.Add(track); return track; }
        public void Update(Track track) { int i = _tracks.FindIndex(t => t.Id == track.Id); if (i >= 0) _tracks[i] = track; }
        public void Remove(int id) => _tracks.RemoveAll(t => t.Id == id);
    }

    private sealed class FakeFileService : IFileService
    {
        public bool Exists(string path) => true;
        public OperationResult Copy(string sourcePath, string targetPath, bool overwrite) => OperationResult.Success("ok");
        public string GetFileName(string path) => Path.GetFileName(path) ?? "track.mp3";
        public OperationResult Delete(string path) => OperationResult.Success("deleted");
    }

    private sealed class FakeSaveFileDialogService : ISaveFileDialogService
    {
        public string? PickSavePath(string suggestedFileName) => null;
    }

    private sealed class FakeAudioPlayerService : IAudioPlayerService
    {
        public event EventHandler<string>? MediaOpened { add { } remove { } }
        public event EventHandler? MediaEnded { add { } remove { } }
        public event EventHandler<string>? MediaFailed { add { } remove { } }

        public bool IsPlaying => false;
        public TimeSpan Position { get; set; }
        public TimeSpan Duration => TimeSpan.FromSeconds(100);
        public double Volume { get; set; } = 1;
        public bool IsMuted { get; set; }

        public OperationResult Open(string filePath) => OperationResult.Success("opened");
        public OperationResult Play() => OperationResult.Success("playing");
        public void Pause() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class FakeListeningHistoryRepository : IListeningHistoryRepository
    {
        private readonly List<PlaybackEntry> _entries = new();
        public IReadOnlyList<PlaybackEntry> GetRecent(int limit = 50) => _entries.OrderByDescending(e => e.PlayedAt).Take(limit).ToList();
        public IReadOnlyList<PlaybackEntry> GetAll() => _entries.OrderByDescending(e => e.PlayedAt).ToList();
        public IReadOnlyList<ListeningStats> GetTop(int limit = 50) => Array.Empty<ListeningStats>();
        public IReadOnlyList<PlaybackEntry> GetRecentUnique(int limit = 50) => Array.Empty<PlaybackEntry>();
        public IReadOnlyList<Track> GetNeverPlayed() => Array.Empty<Track>();
        public void Append(PlaybackEntry entry) => _entries.Add(entry);
    }

    private sealed class FakePlayerSettingsRepository : IPlayerSettingsRepository
    {
        public PlayerSettings Load() => PlayerSettings.Default;
        public void Save(PlayerSettings settings) { }
        public int? LoadActiveViewIndex() => null;
        public void SaveActiveView(MainViewMode view) { }
    }
}
