using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Configurations;

internal sealed class PlaylistEntityConfiguration : IEntityTypeConfiguration<PlaylistEntity>
{
    public void Configure(EntityTypeBuilder<PlaylistEntity> b)
    {
        b.ToTable("Playlists");
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(p => p.CreatedAtUtc);
    }
}
