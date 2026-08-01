using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .IsRequired();
        builder.Property(e => e.StartAt)
            .HasColumnName("start_at");
        builder.Property(e => e.Location)
            .HasColumnName("location");
        builder.Property(e => e.NotifyDayBefore)
            .HasColumnName("notify_day_before")
            .HasDefaultValue(false);
        builder.Property(e => e.NotifyHourBefore)
            .HasColumnName("notify_hour_before")
            .HasDefaultValue(false);
        builder.Property(e => e.CreatedById)
            .HasColumnName("created_by");
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.StartAt)
            .HasDatabaseName("idx_event_start_at");
    }
}
