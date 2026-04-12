import { buildQueryString } from '@/core/infrastructure/http/apiQuery'
import { apiFetch } from '@/infrastructure/apiClient'
import type { IDeviceUiEventRepository } from '@/monitoring/domain/repositories/IDeviceUiEventRepository'
import type { DeviceUiEventRow } from '@/monitoring/domain/entities/deviceUiEvent'
import type { DeviceUiEventQuery } from '@/monitoring/domain/types/queries'
import { parsePagedResult, type PagedResult } from '@/monitoring/domain/types/paged'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'

export class HttpDeviceUiEventRepository implements IDeviceUiEventRepository {
  async list(query: DeviceUiEventQuery): Promise<PagedResult<DeviceUiEventRow>> {
    const qs = buildQueryString({
      device_id: query.deviceId.trim(),
      site_id: query.siteId?.trim() || undefined,
      from: query.from,
      to: query.to,
      page: query.page,
      page_size: query.pageSize,
    })
    const path = `/api/v1/ui-events?${qs}`
    const res = await apiFetch(path)
    if (res.status === 401) {
      throw new UnauthorizedError()
    }
    if (!res.ok) {
      throw new Error(await res.text())
    }
    const data: unknown = await res.json()
    return parsePagedResult<DeviceUiEventRow>(data)
  }
}
