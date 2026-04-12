import type { IDeviceUiEventRepository } from '../../domain/repositories/IDeviceUiEventRepository'
import type { DeviceUiEventRow } from '../../domain/entities/deviceUiEvent'
import type { DeviceUiEventQuery } from '../../domain/types/queries'
import type { PagedResult } from '../../domain/types/paged'

export async function getDeviceUiEventsList(
  repo: IDeviceUiEventRepository,
  query: DeviceUiEventQuery,
): Promise<PagedResult<DeviceUiEventRow>> {
  return repo.list(query)
}
