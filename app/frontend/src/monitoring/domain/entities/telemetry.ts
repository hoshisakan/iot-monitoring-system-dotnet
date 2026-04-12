/** 對齊後端 `TelemetryListItemDto`（JSON snake_case）。 */
export interface TelemetryRow {
  id: number
  device_id: string
  site_id: string
  device_time_utc: string
  server_time_utc: string
  is_sync_back: boolean
  temperature_c?: number | null
  humidity_pct?: number | null
  lux?: number | null
  co2_ppm?: number | null
  temperature_c_scd41?: number | null
  humidity_pct_scd41?: number | null
  pir_active?: boolean | null
  pressure_hpa?: number | null
  gas_resistance_ohm?: number | null
  accel_x?: number | null
  accel_y?: number | null
  accel_z?: number | null
  gyro_x?: number | null
  gyro_y?: number | null
  gyro_z?: number | null
  rssi?: number | null
}

/** 對齊 `SeriesTelemetryResult` / `SeriesMetricDto` / `SeriesPointDto` */
export interface TelemetrySeriesPoint {
  t: string
  v: number | boolean | null
}

export interface TelemetrySeriesItem {
  metric: string
  unit?: string | null
  points: TelemetrySeriesPoint[]
}

export interface TelemetrySeriesResponse {
  device_id: string
  from_utc?: string
  to_utc?: string
  series: TelemetrySeriesItem[]
}
