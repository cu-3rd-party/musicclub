using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class CalendarFeedConfiguration : IEntityTypeConfiguration<CalendarFeed>
{
    public void Configure(EntityTypeBuilder<CalendarFeed> builder)
    {
        builder.ToTable("calendar_feed");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");
        
        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        
        builder.Property(e => e.FeedToken)
            .HasColumnName("feed_token")
            .IsRequired()
            .HasMaxLength(64);
        
        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);
        
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        builder.Property(e => e.RevokedAt)
            .HasColumnName("revoked_at");
        
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Уникальный индекс на токен
        builder.HasIndex(e => e.FeedToken)
            .HasDatabaseName("ix_calendar_feed_token")
            .IsUnique();
        
        // Уникальный индекс на пользователя (один фид на пользователя)
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_calendar_feed_user_id")
            .IsUnique();
    }
}
