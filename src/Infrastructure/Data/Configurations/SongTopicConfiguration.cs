using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class SongTopicConfiguration : IEntityTypeConfiguration<SongTopic>
{
    public void Configure(EntityTypeBuilder<SongTopic> builder)
    {
        builder.ToTable("song_topic");

        builder.HasKey(t => t.SongId);
        builder
            .Property(t => t.SongId)
            .HasColumnName("song_id");

        builder
            .Property(t => t.TopicId)
            .HasColumnName("topic_id");
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
            .WithOne()
            .HasForeignKey<SongTopic>(t => t.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(t => t.TopicId)
            .HasDatabaseName("idx_song_topic_topic_id");
    }
}
