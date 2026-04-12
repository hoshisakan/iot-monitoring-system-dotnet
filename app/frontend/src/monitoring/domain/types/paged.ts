/** 對齊後端 `PagedResult<T>`（snake_case JSON）。 */
export type PagedResult<T> = {
  items: T[]
  page: number
  page_size: number
  total_count: number
}

export function parsePagedResult<T>(data: unknown): PagedResult<T> {
  if (typeof data !== 'object' || data === null) {
    return { items: [], page: 1, page_size: 0, total_count: 0 }
  }
  const o = data as Record<string, unknown>
  const items = Array.isArray(o.items) ? (o.items as T[]) : []
  const page = typeof o.page === 'number' ? o.page : Number(o.page) || 1
  const page_size = typeof o.page_size === 'number' ? o.page_size : Number(o.page_size) || 0
  const total_count = typeof o.total_count === 'number' ? o.total_count : Number(o.total_count) || 0
  return { items, page, page_size, total_count }
}
