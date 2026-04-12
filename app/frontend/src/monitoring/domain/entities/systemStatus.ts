/** 對齊 `SystemController.SystemStatusItem`（JSON snake_case）。 */
export interface SystemStatusItem {
  container_name: string
  container_id: string
  status: string
  ip: string | null
  health: string
}

export interface SystemStatusResponse {
  items: SystemStatusItem[]
}
