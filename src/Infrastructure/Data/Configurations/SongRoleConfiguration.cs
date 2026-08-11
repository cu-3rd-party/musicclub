using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class SongRoleConfiguration : IEntityTypeConfiguration<SongRole>
{
    public void Configure(EntityTypeBuilder<SongRole> builder)
    {
        builder.ToTable("song_role");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.SongId)
            .HasColumnName("song_id");
        builder.Property(r => r.RoleTitle)
            .HasColumnName("role")
            .IsRequired();

        builder.HasIndex(r => new { r.SongId, r.RoleTitle })
            .IsUnique()
            .HasDatabaseName("song_role_song_id_role_title_unique");

        builder.HasOne(r => r.Song)
            .WithMany(s => s.Roles)
            .HasForeignKey(r => r.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
