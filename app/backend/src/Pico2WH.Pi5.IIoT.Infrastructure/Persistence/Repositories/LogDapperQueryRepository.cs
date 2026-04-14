using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Domain.Common;
using Pico2WH.Pi5.IIoT.Domain.Entities;
using Pico2WH.Pi5.IIoT.Domain.Repositories;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Repositories;

public sealed class LogDapperQueryRepository : ILogQueryRepository
{
    private static readonly Regex SafeIdentifier = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string _schema;

    public LogDapperQueryRepository(IDbConnectionFactory connectionFactory, IOptions<DatabaseOptions> databaseOptions)
    {
        _connectionFactory = connectionFactory;
        var schema = (databaseOptions.Value.DefaultSchema ?? "public").Trim();
        if (!SafeIdentifier.IsMatch(schema))
            throw new InvalidOperationException($"Invalid database schema identifier: '{schema}'.");
        _schema = schema;
    }

    public async Task<(IReadOnlyList<StructuredLogEntry> Items, int TotalCount)> QueryAsync(
        LogQueryFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var whereSql = new StringBuilder(" WHERE 1=1");
        var p = new DynamicParameters();

        BuildWhere(filter, whereSql, p);
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("Limit", pageSize);

        var countSql = $"""
            SELECT COUNT(1)
            FROM "{_schema}"."app_logs"
            {whereSql}
            """;

        var listSql = $"""
            SELECT
                id AS Id,
                device_id AS DeviceId,
                channel AS Channel,
                level AS Level,
                message AS Message,
                payload_json AS PayloadJson,
                source_ip AS SourceIp,
                device_time AS DeviceTimeUtc,
                created_at AS CreatedAtUtc
            FROM "{_schema}"."app_logs"
            {whereSql}
            ORDER BY created_at DESC
            OFFSET @Offset
            LIMIT @Limit
            """;

        await using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, p, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var items = (await conn.QueryAsync<StructuredLogEntry>(
            new CommandDefinition(listSql, p, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        return (items, total);
    }

    public async Task<IReadOnlyList<StructuredLogEntry>> FindByDeviceAsync(
        string deviceId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filter = new LogQueryFilter(fromUtc, toUtc, deviceId, null, null);
        var (items, _) = await QueryAsync(filter, page, pageSize, cancellationToken).ConfigureAwait(false);
        return items;
    }

    private static void BuildWhere(LogQueryFilter filter, StringBuilder whereSql, DynamicParameters p)
    {
        if (filter.FromUtc.HasValue)
        {
            whereSql.Append(" AND created_at >= @FromUtc");
            p.Add("FromUtc", filter.FromUtc.Value);
        }
        if (filter.ToUtc.HasValue)
        {
            whereSql.Append(" AND created_at <= @ToUtc");
            p.Add("ToUtc", filter.ToUtc.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.DeviceId))
        {
            whereSql.Append(" AND device_id = @DeviceId");
            p.Add("DeviceId", filter.DeviceId);
        }
        if (!string.IsNullOrWhiteSpace(filter.Channel))
        {
            whereSql.Append(" AND channel = @Channel");
            p.Add("Channel", filter.Channel);
        }
        if (!string.IsNullOrWhiteSpace(filter.Level))
        {
            whereSql.Append(" AND level = @Level");
            p.Add("Level", filter.Level);
        }
    }
}
