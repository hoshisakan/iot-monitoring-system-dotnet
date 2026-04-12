/** 對齊後端 `LogListItemDto`（JSON snake_case）。 */
export interface DeviceLogRow {
  id: number
  device_id: string | null
  channel: string
  level: string
  message: string
  payload_json: string | null
  source_ip: string | null
  device_time_utc: string | null
  created_at_utc: string
}
