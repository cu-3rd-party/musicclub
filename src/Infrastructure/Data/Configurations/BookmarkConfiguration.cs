using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2048);
    }
}
