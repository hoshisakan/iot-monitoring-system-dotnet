/** 空字串 = 同源（Nginx 或 Vite proxy） */
export function getApiBase(): string {
  const v = import.meta.env.VITE_API_BASE
  return typeof v === 'string' ? v : ''
}
