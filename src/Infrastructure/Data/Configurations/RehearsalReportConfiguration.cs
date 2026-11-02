using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CuMusicClub.Infrastructure.Data.Configurations;

public class RehearsalReportConfiguration : IEntityTypeConfiguration<RehearsalReport>
{
    public void Configure(EntityTypeBuilder<RehearsalReport> builder)
    {
        builder.ToTable("rehearsal_report");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(r => r.PeriodStart).HasColumnName("period_start").IsRequired();
        builder.Property(r => r.PeriodEnd).HasColumnName("period_end").IsRequired();
        builder.Property(r => r.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(r => r.Content).HasColumnName("content").HasColumnType("bytea").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(r => r.PeriodStart).IsUnique().HasDatabaseName("rehearsal_report_period_unique");
    }
}

public class RehearsalReportDeliveryConfiguration : IEntityTypeConfiguration<RehearsalReportDelivery>
{
    public void Configure(EntityTypeBuilder<RehearsalReportDelivery> builder)
    {
        builder.ToTable("rehearsal_report_delivery");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(d => d.ReportId).HasColumnName("report_id").IsRequired();
        builder.Property(d => d.Recipient).HasColumnName("recipient").HasMaxLength(64).IsRequired();
        builder.Property(d => d.TgUserId).HasColumnName("tg_user_id").IsRequired();
        builder.Property(d => d.SentAt).HasColumnName("sent_at").IsRequired();

        builder
            .HasOne(d => d.Report)
            .WithMany(r => r.Deliveries)
            .HasForeignKey(d => d.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(d => new { d.ReportId, d.Recipient })
            .IsUnique()
            .HasDatabaseName("rehearsal_report_delivery_unique");
    }
}
