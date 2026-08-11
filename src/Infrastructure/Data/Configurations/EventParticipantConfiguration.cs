using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{
    public void Configure(EntityTypeBuilder<EventParticipant> builder)
    {
        builder.ToTable("event_participant");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.EventId)
            .HasColumnName("event_id")
            .IsRequired();
        builder.Property(p => p.TrackItemId)
            .HasColumnName("track_item_id");
        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(p => p.Role)
            .HasColumnName("role")
            .IsRequired();
        builder.Property(p => p.JoinedAt)
            .HasColumnName("joined_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(p => p.Event)
            .WithMany(e => e.Participants)
            .HasForeignKey(p => p.EventId)
            .HasConstraintName("fk_event_participant_event")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.TrackItem)
            .WithMany()
            .HasForeignKey(p => new
            {
                p.EventId,
                p.TrackItemId
            })
            .HasPrincipalKey(i => new
            {
                i.EventId,
                i.Id
            })
            .HasConstraintName("fk_event_participant_track_item")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new
            {
                p.EventId,
                p.Role,
                p.UserId,
                p.TrackItemId
            })
            .IsUnique()
            .HasDatabaseName("uniq_event_participation");
        builder.HasIndex(p => p.EventId)
            .HasDatabaseName("idx_event_participant_event");
        builder.HasIndex(p => p.UserId)
            .HasDatabaseName("idx_event_participant_user");
    }
}
