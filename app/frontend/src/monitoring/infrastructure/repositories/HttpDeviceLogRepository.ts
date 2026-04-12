import { buildQueryString } from '@/core/infrastructure/http/apiQuery'
import { apiFetch } from '@/infrastructure/apiClient'
import type { IDeviceLogRepository } from '@/monitoring/domain/repositories/IDeviceLogRepository'
import type { DeviceLogRow } from '@/monitoring/domain/entities/deviceLog'
import type { DeviceLogQuery } from '@/monitoring/domain/types/queries'
import { parsePagedResult, type PagedResult } from '@/monitoring/domain/types/paged'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'

export class HttpDeviceLogRepository implements IDeviceLogRepository {
  async list(query: DeviceLogQuery): Promise<PagedResult<DeviceLogRow>> {
    const qs = buildQueryString({
      device_id: query.deviceId?.trim() || undefined,
      channel: query.channel?.trim() || undefined,
      level: query.level?.trim() || undefined,
      from: query.from,
      to: query.to,
      page: query.page,
      page_size: query.pageSize,
    })
    const path = qs ? `/api/v1/logs?${qs}` : '/api/v1/logs'
    const res = await apiFetch(path)
    if (res.status === 401) {
      throw new UnauthorizedError()
    }
    if (!res.ok) {
      throw new Error(await res.text())
    }
    const data: unknown = await res.json()
    return parsePagedResult<DeviceLogRow>(data)
  }
}
