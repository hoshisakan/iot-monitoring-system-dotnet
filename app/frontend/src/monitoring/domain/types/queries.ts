/** 對齊 `GET /api/v1/telemetry`：`device_id`、時間區間、`page`/`page_size` 為必填語意（device 由 UI 強制）。 */
export type TelemetryQuery = {
  deviceId: string
  /** ISO 8601 或後端可解析之時間字串 */
  from: string
  to: string
  page: number
  pageSize: number
}

/** 對齊 `GET /api/v1/telemetry/series`；`metrics` 須為後端允許之鍵名（如 `temperature_c`）。 */
export type TelemetrySeriesQuery = {
  deviceId: string
  from?: string
  to?: string
  metrics: string[]
  maxPoints?: number
}

/** 對齊 `GET /api/v1/logs` */
export type DeviceLogQuery = {
  page: number
  pageSize: number
  deviceId?: string
  channel?: string
  /** 對齊後端 query `level` */
  level?: string
  from?: string
  to?: string
}

/** 對齊 `GET /api/v1/ui-events`（必填 `device_id`） */
export type DeviceUiEventQuery = {
  deviceId: string
  page: number
  pageSize: number
  siteId?: string
  from?: string
  to?: string
}
