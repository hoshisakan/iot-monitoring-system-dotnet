using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Configurations;

public sealed class DeviceControlAuditConfiguration : IEntityTypeConfiguration<DeviceControlAudit>
{
    public void Configure(EntityTypeBuilder<DeviceControlAudit> builder)
    {
        builder.ToTable("device_control_audits");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.DeviceIdentifier)
            .HasConversion(
                d => d.Value,
                s => new DeviceId(s))
            .HasMaxLength(64)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(a => a.Command).HasMaxLength(64).IsRequired();
        builder.Property(a => a.ValuePercent).HasColumnName("value_percent").IsRequired();
        builder.Property(a => a.Value16Bit).HasColumnName("value_16bit").IsRequired();
        builder.Property(a => a.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(a => a.RequestId).HasMaxLength(128).HasColumnName("request_id").IsRequired();
        builder.HasIndex(a => a.RequestId).IsUnique();
        builder.Property(a => a.Accepted).IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
    }
}
