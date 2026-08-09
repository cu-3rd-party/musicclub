using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_session");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(s => s.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);
        builder.Property(s => s.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);
        builder.Property(s => s.ScreenResolution)
            .HasColumnName("screen_resolution")
            .HasMaxLength(32);
        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder.Property(s => s.LastActivityAt)
            .HasColumnName("last_activity_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("idx_user_session_user_id");
    }
}
