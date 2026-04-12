import type { SystemStatusResponse } from '../../domain/entities/systemStatus'
import type { ISystemStatusRepository } from '../../domain/repositories/ISystemStatusRepository'

export async function getSystemStatus(
  repo: ISystemStatusRepository,
): Promise<SystemStatusResponse> {
  return repo.getStatus()
}
