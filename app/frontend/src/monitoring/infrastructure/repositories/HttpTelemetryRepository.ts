import { buildQueryString } from '@/core/infrastructure/http/apiQuery'
import { apiFetch } from '@/infrastructure/apiClient'
import type { ITelemetryRepository } from '@/monitoring/domain/repositories/ITelemetryRepository'
import type { TelemetryRow, TelemetrySeriesResponse } from '@/monitoring/domain/entities/telemetry'
import type { TelemetryQuery, TelemetrySeriesQuery } from '@/monitoring/domain/types/queries'
import { parsePagedResult, type PagedResult } from '@/monitoring/domain/types/paged'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'

/** 將 UI 舊別名映射為後端 `SupportedMetrics` 鍵名 */
const METRIC_ALIASES: Record<string, string> = {
  temperature: 'temperature_c',
  humidity: 'humidity_pct',
  pressure: 'pressure_hpa',
  temperature_scd41: 'temperature_c_scd41',
  humidity_scd41: 'humidity_pct_scd41',
}

function resolveMetrics(metrics: string[]): string[] {
  return metrics.map((m) => METRIC_ALIASES[m] ?? m)
}

export class HttpTelemetryRepository implements ITelemetryRepository {
  async list(query: TelemetryQuery): Promise<PagedResult<TelemetryRow>> {
    const qs = buildQueryString({
      device_id: query.deviceId.trim(),
      from: query.from,
      to: query.to,
      page: query.page,
      page_size: query.pageSize,
    })
    const path = `/api/v1/telemetry?${qs}`
    const res = await apiFetch(path)
    if (res.status === 401) {
      throw new UnauthorizedError()
    }
    if (!res.ok) {
      throw new Error(await res.text())
    }
    const data: unknown = await res.json()
    return parsePagedResult<TelemetryRow>(data)
  }

  async getSeries(query: TelemetrySeriesQuery): Promise<TelemetrySeriesResponse> {
    const metrics = resolveMetrics(query.metrics)
    const qs = buildQueryString({
      device_id: query.deviceId.trim(),
      from: query.from,
      to: query.to,
      metrics: metrics.join(','),
      max_points: query.maxPoints,
    })
    const path = `/api/v1/telemetry/series?${qs}`
    const res = await apiFetch(path)
    if (res.status === 401) {
      throw new UnauthorizedError()
    }
    if (!res.ok) {
      throw new Error(await res.text())
    }
    return (await res.json()) as TelemetrySeriesResponse
  }
}
