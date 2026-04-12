using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Configurations;

public sealed class AppLogRecordConfiguration : IEntityTypeConfiguration<AppLogRecord>
{
    public void Configure(EntityTypeBuilder<AppLogRecord> builder)
    {
        builder.ToTable("app_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityByDefaultColumn();

        builder.Property(a => a.DeviceId).HasMaxLength(64);
        builder.Property(a => a.Channel).HasMaxLength(32).IsRequired();
        builder.Property(a => a.Level).HasMaxLength(16).IsRequired();
        builder.Property(a => a.Message).IsRequired();
        builder.Property(a => a.PayloadJson).HasColumnType("jsonb");
        builder.Property(a => a.SourceIp).HasMaxLength(64);
        builder.Property(a => a.CreatedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.Channel, a.CreatedAtUtc });
    }
}
