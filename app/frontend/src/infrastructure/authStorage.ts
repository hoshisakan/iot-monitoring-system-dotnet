const ACCESS = 'iiot_access_token'
const REFRESH = 'iiot_refresh_token'

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS)
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH)
}

export function setTokens(access: string, refresh: string): void {
  localStorage.setItem(ACCESS, access)
  localStorage.setItem(REFRESH, refresh)
}

export function clearTokens(): void {
  localStorage.removeItem(ACCESS)
  localStorage.removeItem(REFRESH)
}
