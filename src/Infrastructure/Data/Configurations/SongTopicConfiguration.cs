using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class SongTopicConfiguration : IEntityTypeConfiguration<SongTopic>
{
    public void Configure(EntityTypeBuilder<SongTopic> builder)
    {
        builder.ToTable("song_topic");

        builder.HasKey(t => t.TopicId);
        builder
            .Property(t => t.TopicId)
            .HasColumnName("topic_id");

        builder
            .Property(t => t.SongId)
            .HasColumnName("song_id");

        builder
            .Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder
            .Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder
            .HasOne(t => t.Song)
            .WithOne(s => s.SongTopic)
            .HasForeignKey<SongTopic>(t => t.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(t => t.SongId)
            .IsUnique()
            .HasDatabaseName("idx_song_topic_song_id");
    }
}
