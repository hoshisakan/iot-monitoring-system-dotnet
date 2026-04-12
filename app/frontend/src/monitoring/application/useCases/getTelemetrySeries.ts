import type { ITelemetryRepository } from '../../domain/repositories/ITelemetryRepository'
import type { TelemetrySeriesResponse } from '../../domain/entities/telemetry'
import type { TelemetrySeriesQuery } from '../../domain/types/queries'

export async function getTelemetrySeries(
  repo: ITelemetryRepository,
  query: TelemetrySeriesQuery,
): Promise<TelemetrySeriesResponse> {
  return repo.getSeries(query)
}
