using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class RehearsalBookingConfiguration : IEntityTypeConfiguration<RehearsalBooking>
{
    public void Configure(EntityTypeBuilder<RehearsalBooking> builder)
    {
        builder.ToTable("rehearsal_booking");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(b => b.ScheduledAt)
            .HasColumnType("timestamptz");

        builder.Property(b => b.DurationMinutes)
            .IsRequired()
            .HasDefaultValue(80);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(b => b.RequesterTgUserId)
            .IsRequired();

        builder.Property(b => b.CoachUserId)
            .HasColumnType("uuid");

        builder.Property(b => b.SongId)
            .HasColumnType("uuid");

        builder.Property(b => b.CalDavEventUrl)
            .HasColumnName("cal_dav_event_url")
            .HasMaxLength(2048);

        builder.Property(b => b.CalDavEventETag)
            .HasColumnName("cal_dav_event_etag")
            .HasMaxLength(256);

        builder.Property(b => b.CalendarUrl)
            .HasColumnName("calendar_url")
            .HasMaxLength(2048);

        builder.Property(b => b.IsBotManaged)
            .HasColumnName("is_bot_managed")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("NOW()");

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("NOW()");

        // Индекс для поиска pending-бронирований по дате
        builder.HasIndex(b => new { b.Status, b.ScheduledAt })
            .HasDatabaseName("idx_rehearsal_booking_status_scheduled");

        // Индекс для поиска бронирований по requester
        builder.HasIndex(b => new { b.RequesterTgUserId, b.ScheduledAt })
            .HasDatabaseName("idx_rehearsal_booking_requester_scheduled");

        // Уникальный индекс: одно CalDAV событие = одно бронирование
        builder.HasIndex(b => b.CalDavEventUrl)
            .IsUnique()
            .HasDatabaseName("idx_rehearsal_booking_cal_dav_url")
            .HasFilter("cal_dav_event_url IS NOT NULL");
    }
}
