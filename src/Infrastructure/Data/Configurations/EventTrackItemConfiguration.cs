using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class EventTrackItemConfiguration : IEntityTypeConfiguration<EventTrackItem>
{
    public void Configure(EntityTypeBuilder<EventTrackItem> builder)
    {
        builder.ToTable("event_track_item",
            table => table.HasCheckConstraint("track_item_requires_title",
                "\"song_id\" IS NOT NULL OR \"custom_title\" IS NOT NULL"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.EventId)
            .HasColumnName("event_id")
            .IsRequired();
        builder.Property(i => i.Position)
            .HasColumnName("position")
            .IsRequired();
        builder.Property(i => i.SongId)
            .HasColumnName("song_id");
        builder.Property(i => i.CustomTitle)
            .HasColumnName("custom_title");
        builder.Property(i => i.CustomArtist)
            .HasColumnName("custom_artist");

        builder.HasOne(i => i.Event)
            .WithMany(e => e.TrackItems)
            .HasForeignKey(i => i.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Song)
            .WithMany()
            .HasForeignKey(i => i.SongId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => new
            {
                i.EventId,
                i.Position
            })
            .IsUnique()
            .HasDatabaseName("track_item_position");
        builder.HasAlternateKey(i => new
            {
                i.EventId,
                i.Id
            })
            .HasName("track_item_identity");
    }
}
