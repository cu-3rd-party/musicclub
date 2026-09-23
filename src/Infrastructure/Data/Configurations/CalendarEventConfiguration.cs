using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_event");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");
        
        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        
        builder.Property(e => e.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(255);
        
        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);
        
        builder.Property(e => e.StartAt)
            .HasColumnName("start_at")
            .IsRequired();
        
        builder.Property(e => e.EndAt)
            .HasColumnName("end_at")
            .IsRequired();
        
        builder.Property(e => e.Location)
            .HasColumnName("location")
            .HasMaxLength(255);
        
        builder.Property(e => e.SourceType)
            .HasColumnName("source_type")
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(e => e.SourceId)
            .HasColumnName("source_id");
        
        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .IsRequired()
            .HasConversion<int>();
        
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");
        
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Индекс для быстрого поиска событий пользователя за период
        builder.HasIndex(e => new { e.UserId, e.StartAt, e.EndAt })
            .HasDatabaseName("ix_calendar_event_user_id_period");

        // Индекс для поиска по источнику
        builder.HasIndex(e => new { e.SourceType, e.SourceId })
            .HasDatabaseName("ix_calendar_event_source");

        // Частичный индекс для активных событий
        builder.HasIndex(e => new { e.UserId, e.DeletedAt })
            .HasDatabaseName("ix_calendar_event_active")
            .HasFilter("deleted_at IS NULL");
    }
}
