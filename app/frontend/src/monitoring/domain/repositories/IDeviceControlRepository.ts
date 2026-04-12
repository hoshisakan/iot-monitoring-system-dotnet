import type { DeviceControlRequest, DeviceControlResponse } from '../entities/deviceControl'

export interface IDeviceControlRepository {
  dispatch: (payload: DeviceControlRequest) => Promise<DeviceControlResponse>
}
