import { apiFetch } from '@/infrastructure/apiClient'
import type { DeviceControlRequest, DeviceControlResponse } from '@/monitoring/domain/entities/deviceControl'
import type { IDeviceControlRepository } from '@/monitoring/domain/repositories/IDeviceControlRepository'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'
import { ForbiddenError } from '@/monitoring/infrastructure/errors/ForbiddenError'

export class HttpDeviceControlRepository implements IDeviceControlRepository {
  async dispatch(payload: DeviceControlRequest): Promise<DeviceControlResponse> {
    const body = {
      device_id: payload.device_id,
      command: payload.command,
      value: Math.round(payload.value),
      site_id: payload.site_id,
    }
    const res = await apiFetch('/api/v1/device/control', {
      method: 'POST',
      body: JSON.stringify(body),
    })
    if (res.status === 401) {
      throw new UnauthorizedError()
    }
    if (res.status === 403) {
      throw new ForbiddenError('需要管理員權限才能下發裝置控制。')
    }
    if (!res.ok) {
      throw new Error(await res.text())
    }
    return (await res.json()) as DeviceControlResponse
  }
}
