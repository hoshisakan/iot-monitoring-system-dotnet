using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.ValueObjects;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Configurations;

public sealed class TelemetryReadingConfiguration : IEntityTypeConfiguration<TelemetryReading>
{
    public void Configure(EntityTypeBuilder<TelemetryReading> builder)
    {
        builder.ToTable("telemetry_records");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityByDefaultColumn();

        builder.Property(t => t.DeviceId)
            .HasConversion(
                d => d.Value,
                s => new DeviceId(s))
            .HasMaxLength(64)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(t => t.SiteId).HasMaxLength(64).HasColumnName("site_id").IsRequired();

        builder.Property(t => t.DeviceTimeUtc).HasColumnName("device_time").IsRequired();
        builder.Property(t => t.ServerTimeUtc).HasColumnName("server_time").IsRequired();
        builder.Property(t => t.IsSyncBack).HasColumnName("is_sync_back").IsRequired();

        builder.Property(t => t.TemperatureC).HasColumnName("temperature_c");
        builder.Property(t => t.HumidityPct).HasColumnName("humidity_pct");
        builder.Property(t => t.Lux).HasColumnName("lux");
        builder.Property(t => t.Co2Ppm).HasColumnName("co2_ppm");
        builder.Property(t => t.TemperatureCScd41).HasColumnName("temperature_c_scd41");
        builder.Property(t => t.HumidityPctScd41).HasColumnName("humidity_pct_scd41");
        builder.Property(t => t.PirActive).HasColumnName("pir_active");

        builder.Property(t => t.PressureHpa).HasColumnName("pressure_hpa");
        builder.Property(t => t.GasResistanceOhm).HasColumnName("gas_resistance_ohm");
        builder.Property(t => t.AccelX).HasColumnName("accel_x");
        builder.Property(t => t.AccelY).HasColumnName("accel_y");
        builder.Property(t => t.AccelZ).HasColumnName("accel_z");
        builder.Property(t => t.GyroX).HasColumnName("gyro_x");
        builder.Property(t => t.GyroY).HasColumnName("gyro_y");
        builder.Property(t => t.GyroZ).HasColumnName("gyro_z");
        builder.Property(t => t.RssiDbm).HasColumnName("rssi");

        builder.Property(t => t.RawPayloadJson).HasColumnName("raw_payload").HasColumnType("jsonb");

        builder.HasIndex(t => new { t.DeviceId, t.DeviceTimeUtc, t.IsSyncBack }).IsUnique();

        builder.HasIndex(t => new { t.DeviceId, t.DeviceTimeUtc });
    }
}
