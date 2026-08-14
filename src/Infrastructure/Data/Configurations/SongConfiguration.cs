using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class SongConfiguration : IEntityTypeConfiguration<Song>
{
    public void Configure(EntityTypeBuilder<Song> builder)
    {
        builder.ToTable("song");

        builder.HasKey(s => s.Id);
        builder
            .Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder
            .Property(s => s.Title)
            .HasColumnName("title")
            .IsRequired();
        builder
            .Property(s => s.Artist)
            .HasColumnName("artist")
            .IsRequired();
        builder
            .Property(s => s.Description)
            .HasColumnName("description");
        builder
            .Property(s => s.LinkKind)
            .HasColumnName("link_kind")
            .IsRequired();
        builder
            .Property(s => s.LinkUrl)
            .HasColumnName("link_url")
            .IsRequired();
        builder
            .Property(s => s.CreatedById)
            .HasColumnName("created_by");
        builder
            .Property(s => s.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .HasComment("DEPRECATED: SHOULD BE READONLY. USE thumbnail_data_entry_id");
        builder
            .Property(s => s.ThumbnailDataEntryId)
            .HasColumnName("thumbnail_data_entry_id");
        builder
            .Property(s => s.IsFeatured)
            .HasColumnName("is_featured")
            .HasDefaultValue(false);
        builder
            .Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder
            .Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder
            .HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(s => s.ThumbnailDataEntry)
            .WithMany()
            .HasForeignKey(s => s.ThumbnailDataEntryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
