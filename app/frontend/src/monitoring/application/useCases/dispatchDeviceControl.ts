import type { DeviceControlRequest, DeviceControlResponse } from '../../domain/entities/deviceControl'
import type { IDeviceControlRepository } from '../../domain/repositories/IDeviceControlRepository'

export async function dispatchDeviceControl(
  repo: IDeviceControlRepository,
  payload: DeviceControlRequest,
): Promise<DeviceControlResponse> {
  return repo.dispatch(payload)
}
