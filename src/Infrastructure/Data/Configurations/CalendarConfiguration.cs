using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class CalendarConfiguration : IEntityTypeConfiguration<Calendar>
{
    public void Configure(EntityTypeBuilder<Calendar> builder)
    {
        builder.ToTable("calendar");

        builder.HasKey(c => c.UserId);
        builder
            .Property(c => c.UserId)
            .HasColumnName("user_id");

        builder
            .Property(c => c.CalendarUrl)
            .HasColumnName("calendar_url")
            .IsRequired();
        builder
            .Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder
            .Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder
            .HasOne(c => c.User)
            .WithOne()
            .HasForeignKey<Calendar>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
