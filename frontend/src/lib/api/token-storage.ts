/**
 * Single source of truth for auth tokens. Swap this implementation
 * (e.g. to httpOnly-cookie auth) without touching call sites.
 */
import { logger } from '@/lib/logger'
const ACCESS_TOKEN_KEY = 'auth.accessToken'
const REFRESH_TOKEN_KEY = 'auth.refreshToken'

function readToken(storage: Storage, key: string): string | null {
  try {
    return storage.getItem(key)
  } catch {
    return null
  }
}

function decodeJwtPayload(token: string): Record<string, any> {
  try {
    const base64 = token.split('.')[1]
    if (!base64) return {}
    const normalized = base64.replace(/-/g, '+').replace(/_/g, '/')
    const padded = normalized + '='.repeat((4 - (normalized.length % 4)) % 4)
    return JSON.parse(atob(padded))
  } catch {
    return {}
  }
}

function extractRoleCandidates(payload: Record<string, any>): string[] {
  const keys = [
    'role',
    'roles',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role',
    'roleClaim',
  ]

  const values: string[] = []
  for (const key of keys) {
    const raw = payload[key]
    if (raw == null) continue
    if (Array.isArray(raw)) values.push(...raw.map(String))
    else if (typeof raw === 'string') values.push(...raw.split(',').map((part) => part.trim()).filter(Boolean))
    else values.push(String(raw))
  }
  return [...new Set(values)]
}

export const tokenStorage = {
  isPersistent: () => {
    try {
      return !!readToken(localStorage, ACCESS_TOKEN_KEY) || !!readToken(localStorage, REFRESH_TOKEN_KEY)
    } catch {
      return false
    }
  },
  getAccess: () => {
    try {
      return readToken(sessionStorage, ACCESS_TOKEN_KEY) ?? readToken(localStorage, ACCESS_TOKEN_KEY)
    } catch {
      return null
    }
  },
  getRefresh: () => {
    try {
      return readToken(sessionStorage, REFRESH_TOKEN_KEY) ?? readToken(localStorage, REFRESH_TOKEN_KEY)
    } catch {
      return null
    }
  },
  /**
   * Access token survives reloads by default in sessionStorage.
   * If rememberMe is true we persist to localStorage.
   * Refresh token remains in the HttpOnly cookie managed by the backend.
   */
  set: (access: string, refresh?: string, persistent = false) => {
    try {
      const storage = persistent ? localStorage : sessionStorage
      const opposite = persistent ? sessionStorage : localStorage

      storage.setItem(ACCESS_TOKEN_KEY, access)
      if (refresh) storage.setItem(REFRESH_TOKEN_KEY, refresh)

      // Keep only the active scope to avoid stale auth state on reload.
      opposite.removeItem(ACCESS_TOKEN_KEY)
      opposite.removeItem(REFRESH_TOKEN_KEY)
    } catch (e) {
      logger.error('tokenStorage.set failed', e)
    }
  },
  clear: () => {
    try {
      localStorage.removeItem(ACCESS_TOKEN_KEY)
      localStorage.removeItem(REFRESH_TOKEN_KEY)
      sessionStorage.removeItem(ACCESS_TOKEN_KEY)
      sessionStorage.removeItem(REFRESH_TOKEN_KEY)
    } catch (e) {
      logger.error('tokenStorage.clear failed', e)
    }
  },
}
