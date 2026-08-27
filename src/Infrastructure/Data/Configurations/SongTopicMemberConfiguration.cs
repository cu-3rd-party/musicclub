using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class SongTopicMemberConfiguration : IEntityTypeConfiguration<SongTopicMember>
{
    public void Configure(EntityTypeBuilder<SongTopicMember> builder)
    {
        builder.ToTable("song_topic_member");

        builder
            .Property(t => t.Id)
            .HasColumnName("id");
        builder
            .HasKey(t => t.Id);

        builder
            .Property(t => t.TopicId)
            .HasColumnName("topic_id");

        builder
            .Property(t => t.UserId)
            .HasColumnName("user_id");

        builder
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(t => t.Topic)
            .WithMany(t => t.TopicMembers)
            .HasForeignKey(t => t.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
