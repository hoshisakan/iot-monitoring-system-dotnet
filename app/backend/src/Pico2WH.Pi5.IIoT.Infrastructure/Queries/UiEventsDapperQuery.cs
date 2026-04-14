using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Options;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Infrastructure.Persistence;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Queries;

public sealed class UiEventsDapperQuery : IUiEventsQuery
{
    private static readonly Regex SafeIdentifier = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string _schema;

    public UiEventsDapperQuery(IDbConnectionFactory connectionFactory, IOptions<DatabaseOptions> databaseOptions)
    {
        _connectionFactory = connectionFactory;
        var schema = (databaseOptions.Value.DefaultSchema ?? "public").Trim();
        if (!SafeIdentifier.IsMatch(schema))
            throw new InvalidOperationException($"Invalid database schema identifier: '{schema}'.");
        _schema = schema;
    }

    public async Task<PagedResult<UiEventListItemDto>> QueryAsync(
        string? deviceId,
        string? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var whereSql = new StringBuilder(" WHERE 1=1");
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            whereSql.Append(" AND device_id = @DeviceId");
            p.Add("DeviceId", deviceId);
        }
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            whereSql.Append(" AND site_id = @SiteId");
            p.Add("SiteId", siteId);
        }
        if (fromUtc.HasValue)
        {
            whereSql.Append(" AND device_time >= @FromUtc");
            p.Add("FromUtc", fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            whereSql.Append(" AND device_time <= @ToUtc");
            p.Add("ToUtc", toUtc.Value);
        }

        p.Add("Offset", (page - 1) * pageSize);
        p.Add("Limit", pageSize);

        var countSql = $"""
            SELECT COUNT(1)
            FROM "{_schema}"."device_ui_events"
            {whereSql}
            """;

        var listSql = $"""
            SELECT
                event_id AS EventId,
                device_id AS DeviceId,
                device_time AS DeviceTimeUtc,
                event_type AS EventType,
                event_value AS EventValue,
                channel AS Channel,
                site_id AS SiteId
            FROM "{_schema}"."device_ui_events"
            {whereSql}
            ORDER BY device_time DESC
            OFFSET @Offset
            LIMIT @Limit
            """;

        await using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, p, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var items = (await conn.QueryAsync<UiEventListItemDto>(
            new CommandDefinition(listSql, p, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        return new PagedResult<UiEventListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
