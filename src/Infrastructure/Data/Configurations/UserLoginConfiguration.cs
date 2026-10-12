using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        builder.ToTable("user_login");
        builder.ToTable(t => t.HasComment("Привязки пользователей к внешним провайдерам входа (OIDC)"));

        builder.HasKey(l => l.Id);
        builder
            .Property(l => l.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder
            .Property(l => l.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder
            .Property(l => l.Provider)
            .HasColumnName("provider")
            .HasMaxLength(64)
            .IsRequired();
        builder
            .Property(l => l.Subject)
            .HasColumnName("subject")
            .HasMaxLength(255)
            .IsRequired()
            .HasComment("Claim sub провайдера");
        builder
            .Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder
            .Property(l => l.LastUsedAt)
            .HasColumnName("last_used_at");

        builder
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(l => new { l.Provider, l.Subject })
            .IsUnique()
            .HasDatabaseName("ux_user_login_provider_subject");
        builder
            .HasIndex(l => l.UserId)
            .HasDatabaseName("idx_user_login_user_id");
    }
}
