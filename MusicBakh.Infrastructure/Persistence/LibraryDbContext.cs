using Microsoft.EntityFrameworkCore;
using MusicBakh.Infrastructure.Persistence.Configurations;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence;

/// <summary>
/// Контекст локальной библиотеки MusicBakh. В этой итерации содержит только треки,
/// в Task 11 к нему присоединится KV-стор настроек.
/// </summary>
public sealed class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    internal DbSet<TrackEntity> Tracks => Set<TrackEntity>();
    internal DbSet<ListeningHistoryEntryEntity> ListeningHistory => Set<ListeningHistoryEntryEntity>();
    internal DbSet<KeyValueEntryEntity> KeyValueStore => Set<KeyValueEntryEntity>();
    internal DbSet<TagEntity> Tags => Set<TagEntity>();
    internal DbSet<TrackTagEntity> TrackTags => Set<TrackTagEntity>();
    internal DbSet<PlaylistEntity> Playlists => Set<PlaylistEntity>();
    internal DbSet<PlaylistTrackEntity> PlaylistTracks => Set<PlaylistTrackEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TrackEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ListeningHistoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new KeyValueEntryConfiguration());
        modelBuilder.ApplyConfiguration(new TagEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TrackTagEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlaylistEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PlaylistTrackEntityConfiguration());
    }
}
