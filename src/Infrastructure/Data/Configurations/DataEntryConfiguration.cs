using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class DataEntryConfiguration : IEntityTypeConfiguration<DataEntry>
{
    public void Configure(EntityTypeBuilder<DataEntry> builder)
    {
        builder.ToTable("data_entry");
        builder.ToTable(x => x.HasComment("айноды с каким-то содержанием. жалкая замена s3 ибо мне лень инфру настраивать"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .HasComment("полезная информация")
            .HasColumnType("bytea")
            .IsRequired();

        builder
            .Property(x => x.Hash)
            .HasComment("дедубликация")
            .HasColumnType("bytea")
            .IsRequired();

        builder
            .HasIndex(x => x.Hash)
            .IsUnique();

        builder
            .Property(x => x.ContentType)
            .HasComment("этому нельзя доверять, но мы будем")
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(x => x.Size)
            .HasComment("опциональная метаинфа")
            .IsRequired();
    }
}
