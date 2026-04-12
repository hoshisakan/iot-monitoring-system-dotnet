import type { IDeviceLogRepository } from '../../domain/repositories/IDeviceLogRepository'
import type { DeviceLogRow } from '../../domain/entities/deviceLog'
import type { DeviceLogQuery } from '../../domain/types/queries'
import type { PagedResult } from '../../domain/types/paged'

export async function getDeviceLogsList(
  repo: IDeviceLogRepository,
  query: DeviceLogQuery,
): Promise<PagedResult<DeviceLogRow>> {
  return repo.list(query)
}
