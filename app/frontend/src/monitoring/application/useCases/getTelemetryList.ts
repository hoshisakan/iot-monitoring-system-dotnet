import type { ITelemetryRepository } from '../../domain/repositories/ITelemetryRepository'
import type { TelemetryRow } from '../../domain/entities/telemetry'
import type { TelemetryQuery } from '../../domain/types/queries'
import type { PagedResult } from '../../domain/types/paged'

export async function getTelemetryList(
  repo: ITelemetryRepository,
  query: TelemetryQuery,
): Promise<PagedResult<TelemetryRow>> {
  return repo.list(query)
}
