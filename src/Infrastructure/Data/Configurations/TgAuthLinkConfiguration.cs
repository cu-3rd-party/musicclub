using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class TgAuthLinkConfiguration : IEntityTypeConfiguration<TgAuthLink>
{
    public void Configure(EntityTypeBuilder<TgAuthLink> builder)
    {
        builder.ToTable("tg_auth_link");
        builder.ToTable(t => t.HasComment("Диплинки для захода в систему"));

        builder.HasKey(a => a.Id);
        builder
            .Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder
            .Property(a => a.TgUserId)
            .HasComment("Айди кто использовал ссылку");

        builder.HasIndex(a => a.TgUserId);
    }
}
