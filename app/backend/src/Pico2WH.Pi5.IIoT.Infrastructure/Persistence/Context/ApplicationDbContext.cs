using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Models;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;

public sealed class ApplicationDbContext : DbContext
{
    private readonly string _defaultSchema;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IOptions<DatabaseOptions> databaseOptions)
        : base(options)
    {
        var s = databaseOptions.Value.DefaultSchema;
        _defaultSchema = string.IsNullOrWhiteSpace(s) ? "public" : s.Trim();
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<TelemetryReading> TelemetryReadings => Set<TelemetryReading>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<DeviceControlAudit> DeviceControlAudits => Set<DeviceControlAudit>();

    public DbSet<DeviceUiEventRecord> DeviceUiEvents => Set<DeviceUiEventRecord>();

    public DbSet<AppLogRecord> AppLogs => Set<AppLogRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_defaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
