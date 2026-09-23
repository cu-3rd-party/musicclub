using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("app_user");
        builder.HasKey(u => u.Id);

        builder
            .Property(u => u.Id)
            .HasColumnName("Id");
        builder
            .Property(u => u.UserName)
            .HasColumnName("UserName");
        builder
            .Property(u => u.Email)
            .HasColumnName("Email");
        builder
            .Property(u => u.PasswordHash)
            .HasColumnName("PasswordHash");
        builder
            .Property(u => u.TgUserId)
            .HasColumnName("TgUserId");
        builder
            .Property(u => u.IsChatMember)
            .HasColumnName("IsChatMember")
            .HasDefaultValue(false);
        builder
            .Property(u => u.DisplayName)
            .HasColumnName("DisplayName");
        builder
            .Property(u => u.AvatarUrl)
            .HasColumnName("AvatarUrl");
        builder
            .Property(u => u.YandexLogin)
            .HasColumnName("YandexLogin")
            .HasMaxLength(100);
        builder
            .Property(u => u.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasDefaultValueSql("NOW()");
        builder
            .Property(u => u.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasDefaultValueSql("NOW()");

        builder
            .HasIndex(u => u.TgUserId)
            .IsUnique()
            .HasDatabaseName("idx_application_user_tg_user_id");
        
        builder
            .HasIndex(u => u.YandexLogin)
            .HasDatabaseName("idx_application_user_yandex_login");
    }
}