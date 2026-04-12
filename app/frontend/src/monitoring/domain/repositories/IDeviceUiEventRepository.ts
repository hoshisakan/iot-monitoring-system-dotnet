import type { DeviceUiEventRow } from '../entities/deviceUiEvent'
import type { DeviceUiEventQuery } from '../types/queries'
import type { PagedResult } from '../types/paged'

export interface IDeviceUiEventRepository {
  list: (query: DeviceUiEventQuery) => Promise<PagedResult<DeviceUiEventRow>>
}
