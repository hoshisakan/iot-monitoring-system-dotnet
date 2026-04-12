import type { TelemetryRow, TelemetrySeriesResponse } from '../entities/telemetry'
import type { TelemetryQuery, TelemetrySeriesQuery } from '../types/queries'
import type { PagedResult } from '../types/paged'

export interface ITelemetryRepository {
  list: (query: TelemetryQuery) => Promise<PagedResult<TelemetryRow>>
  getSeries: (query: TelemetrySeriesQuery) => Promise<TelemetrySeriesResponse>
}
