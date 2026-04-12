using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pico2WH.Pi5.IIoT.Application.Common.Models;
using Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.ListTelemetry;
using Pico2WH.Pi5.IIoT.Application.Features.Telemetry.Queries.SeriesTelemetry;

namespace Pico2WH.Pi5.IIoT.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private static readonly HashSet<string> SupportedMetrics = new(StringComparer.OrdinalIgnoreCase)
    {
        "temperature_c",
        "humidity_pct",
        "lux",
        "co2_ppm",
        "temperature_c_scd41",
        "humidity_pct_scd41",
        "pir_active",
        "pressure_hpa",
        "gas_resistance_ohm",
        "accel_x",
        "accel_y",
        "accel_z",
        "gyro_x",
        "gyro_y",
        "gyro_z",
        "rssi"
    };

    private readonly ISender _mediator;

    public TelemetryController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TelemetryListItemDto>>> List(
        [FromQuery] string device_id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var toUtc = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var fromUtc = from?.ToUniversalTime() ?? toUtc.AddDays(-1);
        if (fromUtc >= toUtc)
            return BadRequest(new { error = new { code = "INVALID_RANGE", message = "from 必須小於 to。" } });

        var result = await _mediator
            .Send(new ListTelemetryQuery(device_id, fromUtc, toUtc, page, pageSize), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("series")]
    public async Task<ActionResult<SeriesTelemetryResult>> Series(
        [FromQuery] string device_id,
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] string metrics,
        [FromQuery(Name = "max_points")] int? maxPoints,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseInstant(from, out var fromUtc) || !TryParseInstant(to, out var toUtc))
            return BadRequest(new { error = new { code = "INVALID_DATETIME", message = "from/to 無法解析。" } });

        if (fromUtc >= toUtc)
            return BadRequest(new { error = new { code = "INVALID_RANGE", message = "from 必須小於 to。" } });

        var metricList = metrics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var invalid = metricList.Where(m => !SupportedMetrics.Contains(m)).ToList();
        if (invalid.Count > 0)
            return BadRequest(new { error = new { code = "INVALID_METRICS", invalid_metrics = invalid } });

        var result = await _mediator
            .Send(new SeriesTelemetryQuery(device_id, metricList, fromUtc, toUtc, maxPoints), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    private static bool TryParseInstant(string s, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(s))
            return false;

        if (long.TryParse(s, out var unixMs))
        {
            utc = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
        {
            utc = dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
            return true;
        }

        return false;
    }
}
