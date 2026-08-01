using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class CalendarAttachStateConfiguration : IEntityTypeConfiguration<CalendarAttachState>
{
    public void Configure(EntityTypeBuilder<CalendarAttachState> builder)
    {
        builder.ToTable("calendar_attach_state");

        builder.HasKey(s => s.TgUserId);
        builder.Property(s => s.TgUserId)
            .HasColumnName("tg_user_id")
            .ValueGeneratedNever();

        builder.Property(s => s.State)
            .HasColumnName("state")
            .IsRequired();
        builder.Property(s => s.PendingUserId)
            .HasColumnName("pending_user_id");
        builder.Property(s => s.PendingEmail)
            .HasColumnName("pending_email");
        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");
    }
}
