using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.TgUserId)
            .HasColumnName("TgUserId");
        builder.Property(u => u.IsChatMember)
            .HasColumnName("IsChatMember")
            .HasDefaultValue(false);
        builder.Property(u => u.DisplayName)
            .HasColumnName("DisplayName");
        builder.Property(u => u.AvatarUrl)
            .HasColumnName("AvatarUrl");
        builder.Property(u => u.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasDefaultValueSql("NOW()");
        builder.Property(u => u.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(u => u.TgUserId)
            .IsUnique()
            .HasDatabaseName("idx_application_user_tg_user_id");
    }
}
