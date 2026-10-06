using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

/// <summary>EF-конфигурация попыток угадать логин Яндекса (таблица <c>yandex_login_guess</c>).</summary>
public class YandexLoginGuessConfiguration : IEntityTypeConfiguration<YandexLoginGuess>
{
    public void Configure(EntityTypeBuilder<YandexLoginGuess> builder)
    {
        builder.ToTable("yandex_login_guess");

        builder.HasKey(g => g.UserId);
        builder
            .Property(g => g.UserId)
            .HasColumnName("user_id");

        builder
            .Property(g => g.AttemptedAt)
            .HasColumnName("attempted_at")
            .IsRequired();

        builder
            .Property(g => g.Query)
            .HasColumnName("query")
            .IsRequired();

        builder
            .Property(g => g.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder
            .Property(g => g.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder
            .Property(g => g.Error)
            .HasColumnName("error");

        builder
            .HasOne(g => g.User)
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
