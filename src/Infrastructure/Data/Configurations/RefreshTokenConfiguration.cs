using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(t => t.Token)
            .HasColumnName("token")
            .IsRequired();
        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();
        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("idx_refresh_tokens_token");
        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("idx_refresh_tokens_user_id");
    }
}
