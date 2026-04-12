import { getApiBase } from './apiBase'
import { clearTokens, getAccessToken, getRefreshToken, setTokens } from './authStorage'

async function tryRefresh(): Promise<boolean> {
  const refresh = getRefreshToken()
  if (!refresh) return false
  const r = await fetch(`${getApiBase()}/api/v1/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refresh_token: refresh }),
  })
  if (!r.ok) {
    clearTokens()
    return false
  }
  const data = (await r.json()) as {
    access_token: string
    refresh_token: string
  }
  setTokens(data.access_token, data.refresh_token)
  return true
}

export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  const token = getAccessToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }
  if (init.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  let res = await fetch(`${getApiBase()}${path}`, { ...init, headers })

  if (res.status === 401 && path !== '/api/v1/auth/refresh' && path !== '/api/v1/auth/login') {
    const ok = await tryRefresh()
    if (ok) {
      const h2 = new Headers(init.headers)
      const t2 = getAccessToken()
      if (t2) h2.set('Authorization', `Bearer ${t2}`)
      if (init.body && !h2.has('Content-Type')) {
        h2.set('Content-Type', 'application/json')
      }
      res = await fetch(`${getApiBase()}${path}`, { ...init, headers: h2 })
    }
  }

  return res
}
