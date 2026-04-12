import type { DeviceLogRow } from '../entities/deviceLog'
import type { DeviceLogQuery } from '../types/queries'
import type { PagedResult } from '../types/paged'

export interface IDeviceLogRepository {
  list: (query: DeviceLogQuery) => Promise<PagedResult<DeviceLogRow>>
}
