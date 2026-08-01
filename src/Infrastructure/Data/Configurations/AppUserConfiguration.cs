using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Username)
            .HasColumnName("username")
            .IsRequired();
        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash");
        builder.Property(u => u.TgUserId)
            .HasColumnName("tg_user_id");
        builder.Property(u => u.IsChatMember)
            .HasColumnName("is_chat_member")
            .HasDefaultValue(false);
        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name")
            .IsRequired();
        builder.Property(u => u.Email)
            .HasColumnName("email");
        builder.Property(u => u.AvatarUrl)
            .HasColumnName("avatar_url");
        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.TgUserId).IsUnique();
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("idx_app_user_email");
    }
}
