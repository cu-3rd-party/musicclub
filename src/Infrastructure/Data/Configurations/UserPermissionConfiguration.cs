using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("user_permissions");

        builder.HasKey(p => p.UserId);
        builder.Property(p => p.UserId)
            .HasColumnName("user_id");

        builder.Property(p => p.EditOwnParticipation)
            .HasColumnName("edit_own_participation")
            .HasDefaultValue(false);
        builder.Property(p => p.EditAnyParticipation)
            .HasColumnName("edit_any_participation")
            .HasDefaultValue(false);
        builder.Property(p => p.EditOwnSongs)
            .HasColumnName("edit_own_songs")
            .HasDefaultValue(false);
        builder.Property(p => p.EditAnySongs)
            .HasColumnName("edit_any_songs")
            .HasDefaultValue(false);
        builder.Property(p => p.EditEvents)
            .HasColumnName("edit_events")
            .HasDefaultValue(false);
        builder.Property(p => p.EditTracklists)
            .HasColumnName("edit_tracklists")
            .HasDefaultValue(false);
        builder.Property(p => p.EditFeaturedSongs)
            .HasColumnName("edit_featured_songs")
            .HasDefaultValue(false);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
