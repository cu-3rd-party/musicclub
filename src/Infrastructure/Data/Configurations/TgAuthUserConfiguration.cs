using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class TgAuthUserConfiguration : IEntityTypeConfiguration<TgAuthUser>
{
    public void Configure(EntityTypeBuilder<TgAuthUser> builder)
    {
        builder.ToTable("tg_auth_user");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(u => u.TgUserId)
            .HasColumnName("tg_user_id");
        builder.Property(u => u.Success)
            .HasColumnName("success")
            .HasDefaultValue(false);

        builder.HasOne(u => u.User)
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => u.TgUserId)
            .IsUnique()
            .HasDatabaseName("idx_tg_auth_session_user");
    }
}
