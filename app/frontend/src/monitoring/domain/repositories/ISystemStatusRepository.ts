import type { SystemStatusResponse } from '../entities/systemStatus'

export interface ISystemStatusRepository {
  getStatus: () => Promise<SystemStatusResponse>
}
