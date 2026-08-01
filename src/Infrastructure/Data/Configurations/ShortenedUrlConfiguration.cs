using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class ShortenedUrlConfiguration : IEntityTypeConfiguration<ShortenedUrl>
{
    public void Configure(EntityTypeBuilder<ShortenedUrl> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalUrl)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.ShortCode)
            .HasMaxLength(8)
            .IsRequired();

        builder.HasIndex(x => x.ShortCode)
            .IsUnique();
    }
}
