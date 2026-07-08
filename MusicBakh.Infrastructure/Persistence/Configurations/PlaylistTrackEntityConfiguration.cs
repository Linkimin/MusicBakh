using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

internal sealed class PlaylistTrackEntityConfiguration : IEntityTypeConfiguration<PlaylistTrackEntity>
{
    public void Configure(EntityTypeBuilder<PlaylistTrackEntity> b)
    {
        b.ToTable("PlaylistTracks");
        b.HasKey(pt => pt.Id);
        b.HasIndex(pt => new { pt.PlaylistId, pt.Position });

        b.HasOne(pt => pt.Playlist)
            .WithMany()
            .HasForeignKey(pt => pt.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(pt => pt.Track)
            .WithMany()
            .HasForeignKey(pt => pt.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
