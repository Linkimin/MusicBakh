using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Persistence;

public sealed class SqlitePlaylistRepositoryTests
{
    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    private static int SeedTrack(MigratedSqliteDbContextFactory f, string title)
    {
        using var ctx = f.CreateContext();
        var e = new TrackEntity { Title = title, Artist = "A", FilePath = title + ".mp3" };
        ctx.Tracks.Add(e);
        ctx.SaveChanges();
        return e.Id;
    }

    [Fact]
    public void Create_And_GetAll_Orders_By_CreatedAt_Desc()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var clock = new FixedClock();
        var repo = new SqlitePlaylistRepository(f.CreateContext, clock);

        var p1 = repo.Create("First");
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var p2 = repo.Create("Second");

        var all = repo.GetAll();
        Assert.Equal(new[] { "Second", "First" }, all.Select(p => p.Name).ToArray());
        Assert.True(p1.Id > 0 && p2.Id > 0);
    }

    [Fact]
    public void AddTracks_Preserves_Order_And_Allows_Duplicates()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int a = SeedTrack(f, "a"), b = SeedTrack(f, "b");
        var p = repo.Create("P");

        repo.AddTracks(p.Id, new[] { a, b, a });   // a добавлен дважды

        var tracks = repo.GetTracks(p.Id);
        Assert.Equal(new[] { a, b, a }, tracks.Select(t => t.Id).ToArray());
        Assert.Equal(3, repo.GetTrackCount(p.Id));
    }

    [Fact]
    public void RemoveAt_Compacts_Positions()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int a = SeedTrack(f, "a"), b = SeedTrack(f, "b"), c = SeedTrack(f, "c");
        var p = repo.Create("P");
        repo.AddTracks(p.Id, new[] { a, b, c });

        repo.RemoveAt(p.Id, 1);   // убираем b

        Assert.Equal(new[] { a, c }, repo.GetTracks(p.Id).Select(t => t.Id).ToArray());
    }

    [Fact]
    public void Reorder_Moves_Track_And_Renumbers()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int a = SeedTrack(f, "a"), b = SeedTrack(f, "b"), c = SeedTrack(f, "c");
        var p = repo.Create("P");
        repo.AddTracks(p.Id, new[] { a, b, c });

        repo.Reorder(p.Id, fromPosition: 0, toPosition: 2);   // a в конец

        Assert.Equal(new[] { b, c, a }, repo.GetTracks(p.Id).Select(t => t.Id).ToArray());
    }

    [Fact]
    public void Deleting_Track_From_Library_Cascades_Out_Of_Playlist()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int a = SeedTrack(f, "a"), b = SeedTrack(f, "b");
        var p = repo.Create("P");
        repo.AddTracks(p.Id, new[] { a, b });

        using (var ctx = f.CreateContext())
        {
            ctx.Tracks.Remove(ctx.Tracks.First(t => t.Id == a));
            ctx.SaveChanges();
        }

        Assert.Equal(new[] { b }, repo.GetTracks(p.Id).Select(t => t.Id).ToArray());
    }

    [Fact]
    public void GetTracks_Maps_All_Track_Fields()
    {
        using var f = new MigratedSqliteDbContextFactory();
        var repo = new SqlitePlaylistRepository(f.CreateContext, new FixedClock());
        int id;
        using (var ctx = f.CreateContext())
        {
            var e = new TrackEntity { Title = "T", Artist = "Ar", Album = "Al", FilePath = "t.mp3",
                Year = 2014, TrackNumber = 3, AlbumArtist = "AA", Rating = 4, Reaction = 1, IsBuiltIn = true };
            ctx.Tracks.Add(e); ctx.SaveChanges(); id = e.Id;
        }
        var p = repo.Create("P");
        repo.AddTrack(p.Id, id);

        var t = repo.GetTracks(p.Id).Single();
        Assert.Equal(2014, t.Year);
        Assert.Equal(3, t.TrackNumber);
        Assert.Equal("AA", t.AlbumArtist);
        Assert.Equal(4, t.Rating);
        Assert.True(t.IsBuiltIn);
    }
}
