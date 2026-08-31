using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(s => s.Jti);
        builder
            .Property(s => s.Jti)
            .HasColumnName("jti")
            .IsRequired()
            .HasDefaultValueSql("gen_random_uuid()");
        builder
            .Property(s => s.Sub)
            .HasColumnName("sub")
            .IsRequired()
            .HasDefaultValueSql("gen_random_uuid()");
        builder
            .Property(s => s.Exp)
            .IsRequired()
            .HasColumnName("exp")
            .HasDefaultValueSql("NOW()");
        builder
            .Property(s => s.Iat)
            .IsRequired()
            .HasColumnName("iat")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(s => s.Sub)
            .HasDatabaseName("idx_refresh_token_sub");
        builder.HasIndex(s => s.Iat)
            .HasDatabaseName("idx_refresh_token_iat");
        builder.HasIndex(s => s.Exp)
            .HasDatabaseName("idx_refresh_token_exp");
        builder.HasIndex(s => s.Revoked)
            .HasDatabaseName("idx_refresh_token_revoked");

        builder
            .HasOne(s => s.JtiSession)
            .WithOne(u => u.RefreshToken)
            .HasForeignKey<UserSession>(s => s.RefreshTokenJti)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(s => s.SubUser)
            .WithMany()
            .HasForeignKey(s => s.Sub)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
