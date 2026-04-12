using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Context;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence;

/// <summary>設計時建立 DbContext（<c>dotnet ef</c>）。會讀取啟動專案目錄下的 <c>appsettings*.json</c>（請以 Api 為 <c>--startup-project</c>），並支援環境變數覆寫。</summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json",
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = configuration.GetConnectionString("Default")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=127.0.0.1;Port=5432;Database=pico2wh_dev;Username=postgres;Password=postgres";

        var services = new ServiceCollection();
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        using var provider = services.BuildServiceProvider();
        var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(cs)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options, databaseOptions);
    }
}
