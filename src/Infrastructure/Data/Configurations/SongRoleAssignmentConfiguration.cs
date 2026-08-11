using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class SongRoleAssignmentConfiguration : IEntityTypeConfiguration<SongRoleAssignment>
{
    public void Configure(EntityTypeBuilder<SongRoleAssignment> builder)
    {
        builder.ToTable("song_role_assignment");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.SongId)
            .HasColumnName("song_id")
            .IsRequired();
        builder.Property(a => a.RoleId)
            .HasColumnName("role")
            .IsRequired();
        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(a => a.JoinedAt)
            .HasColumnName("joined_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(a => a.Song)
            .WithMany(s => s.Assignments)
            .HasForeignKey(a => a.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.SongRole)
            .WithOne(r => r.Assignment)
            .HasForeignKey<SongRoleAssignment>(a => a.RoleId)
            .HasConstraintName("song_role_assignment")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new
            {
                a.SongId,
                a.RoleId,
                a.UserId
            })
            .IsUnique()
            .HasDatabaseName("song_role_assignment_unique");
        builder.HasIndex(a => a.SongId)
            .HasDatabaseName("idx_song_role_assignment_song");
        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("idx_song_role_assignment_user");
    }
}
