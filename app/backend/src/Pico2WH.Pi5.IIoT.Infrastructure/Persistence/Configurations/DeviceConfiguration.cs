using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Configurations;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DeviceIdentifier)
            .HasConversion(
                d => d.Value,
                s => new DeviceId(s))
            .HasMaxLength(64)
            .HasColumnName("device_id")
            .IsRequired();

        builder.HasIndex(d => d.DeviceIdentifier).IsUnique();

        builder.Property(d => d.Name).HasMaxLength(128).IsRequired();
        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.LastSeenAtUtc).IsRequired();
        builder.Property(d => d.CreatedAtUtc).IsRequired();
        builder.Property(d => d.UpdatedAtUtc);
    }
}
