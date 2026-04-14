using System.Data.Common;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence;

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly ILogger<NpgsqlConnectionFactory> _logger;

    public NpgsqlConnectionFactory(string connectionString, ILogger<NpgsqlConnectionFactory> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var conn = new NpgsqlConnection(_connectionString);
        try
        {
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            return conn;
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false);
            _logger.LogError("Failed to open PostgreSQL connection for query side.");
            throw;
        }
    }
}
