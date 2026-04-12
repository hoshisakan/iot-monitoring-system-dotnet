/** 對齊後端 `POST /api/v1/device/control` body（目前僅 `set_pwm`）。 */
export interface DeviceControlRequest {
  site_id?: string
  device_id: string
  /** 後端欄位名為 `command` */
  command: 'set_pwm'
  /** 0～100（百分比），對應後端 `DeviceControlRequest.Value` */
  value: number
}

/** 對齊 `DeviceControlAcceptedResponse`（202 Accepted） */
export interface DeviceControlResponse {
  status: string
  device_id: string
  command: string
  value: number
}
