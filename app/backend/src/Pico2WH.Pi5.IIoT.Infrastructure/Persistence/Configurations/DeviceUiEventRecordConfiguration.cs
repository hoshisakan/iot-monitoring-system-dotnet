using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Configurations;

public sealed class DeviceUiEventRecordConfiguration : IEntityTypeConfiguration<DeviceUiEventRecord>
{
    public void Configure(EntityTypeBuilder<DeviceUiEventRecord> builder)
    {
        builder.ToTable("device_ui_events");

        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).UseIdentityByDefaultColumn();

        builder.Property(e => e.DeviceId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DeviceTimeUtc).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.EventValue).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Channel).HasMaxLength(32).IsRequired();
        builder.Property(e => e.SiteId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb");
        builder.Property(e => e.IngestedAtUtc).IsRequired();

        builder.HasIndex(e => new { e.DeviceId, e.DeviceTimeUtc });
        builder.HasIndex(e => new { e.SiteId, e.DeviceId, e.DeviceTimeUtc });
    }
}
