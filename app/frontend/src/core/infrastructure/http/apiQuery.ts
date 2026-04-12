/** Build `key=value&...` omitting undefined / null / empty string. */
export function buildQueryString(params: Record<string, string | number | boolean | undefined | null>): string {
  const parts: string[] = []
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null) continue
    const s = String(value).trim()
    if (s === '') continue
    parts.push(`${encodeURIComponent(key)}=${encodeURIComponent(s)}`)
  }
  return parts.join('&')
}
