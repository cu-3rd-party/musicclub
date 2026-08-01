using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class SongRoleConfiguration : IEntityTypeConfiguration<SongRole>
{
    public void Configure(EntityTypeBuilder<SongRole> builder)
    {
        builder.ToTable("song_role");

        builder.HasKey(r => new { r.SongId, r.Role });
        builder.Property(r => r.SongId)
            .HasColumnName("song_id");
        builder.Property(r => r.Role)
            .HasColumnName("role")
            .IsRequired();

        builder.HasOne(r => r.Song)
            .WithMany(s => s.Roles)
            .HasForeignKey(r => r.SongId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
