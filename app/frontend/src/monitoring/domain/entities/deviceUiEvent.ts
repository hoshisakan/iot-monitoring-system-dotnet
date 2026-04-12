/** 對齊後端 `UiEventListItemDto`（JSON snake_case）。 */
export interface DeviceUiEventRow {
  event_id: number
  device_id: string
  device_time_utc: string
  event_type: string
  event_value: string
  channel: string
  site_id: string
}
