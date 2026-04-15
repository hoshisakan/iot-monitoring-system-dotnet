import { apiFetch } from '@/infrastructure/apiClient'
import type { SystemStatusResponse } from '@/monitoring/domain/entities/systemStatus'
import type { ISystemStatusRepository } from '@/monitoring/domain/repositories/ISystemStatusRepository'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'
import { ForbiddenError } from '@/monitoring/infrastructure/errors/ForbiddenError'

export class HttpSystemStatusRepository implements ISystemStatusRepository {
  async getStatus(): Promise<SystemStatusResponse> {
    const res = await apiFetch('/api/v1/system/status')
    if (res.status === 401) {
      throw new UnauthorizedError()
    }
    if (res.status === 403) {
      throw new ForbiddenError('需要管理員權限才能查看系統狀態。')
    }
    if (!res.ok) {
      throw new Error(await res.text())
    }
    const data: unknown = await res.json()
    if (typeof data !== 'object' || data === null) {
      return { items: [] }
    }
    const o = data as Record<string, unknown>
    const items = Array.isArray(o.items) ? (o.items as SystemStatusResponse['items']) : []
    const warning_code =
      typeof o.warning_code === 'string' || o.warning_code === null
        ? (o.warning_code as string | null)
        : null
    const warning_message =
      typeof o.warning_message === 'string' || o.warning_message === null
        ? (o.warning_message as string | null)
        : null
    return { items, warning_code, warning_message }
  }
}
