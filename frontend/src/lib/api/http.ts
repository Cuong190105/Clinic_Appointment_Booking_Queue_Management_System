import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import { env } from '@/config/env'
import { tokenStorage } from './token-storage'
import { logger } from '@/lib/logger'

export interface ApiErrorBody {
  message?: string
  code?: string
  errors?: Record<string, string[]>
  // Support backend ApiResponse envelope
  errorMessages?: string[]
  result?: any
}

/** Normalized error the UI/stores can rely on — never a raw AxiosError. */
export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly fieldErrors?: Record<string, string[]>

  constructor(status: number, body?: ApiErrorBody) {
    // Prefer ApiResponse.errorMessages (array) -> join, then body.message, then fallback
    const msgFromEnvelope = body?.errorMessages && body.errorMessages.length > 0 ? body.errorMessages.join(' | ') : undefined
    const message = msgFromEnvelope ?? body?.message ?? `Request failed with status ${status}`

    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = body?.code

    // If the backend returned structured errors under result (e.g., ModelState), try to extract field map
    if (body?.errors && typeof body.errors === 'object') {
      this.fieldErrors = body.errors
    } else if (body?.result && typeof body.result === 'object') {
      // attempt to look for ValidationProblemDetails-like shape
      const res = body.result
      if (res?.errors && typeof res.errors === 'object') {
        this.fieldErrors = res.errors as Record<string, string[]>
      }
    }
  }
}

export const http: AxiosInstance = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 15_000,
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
})

// Request: attach bearer token.
http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStorage.getAccess()
  if (token) {
    config.headers = config.headers ?? {}
    ;(config.headers as Record<string, any>)['Authorization'] = 'Bearer ' + token
  }
  return config
})

// Response: refresh on 401 when the backend uses an HttpOnly refresh cookie.
let refreshing: Promise<void> | null = null

async function refreshSession(): Promise<void> {
  const persistent = tokenStorage.isPersistent()
  const { data } = await axios.post<any>(
    `${env.apiBaseUrl}/auth/refresh`,
    {},
    { withCredentials: true },
  )

  const newAccessToken = data?.result?.accessToken ?? data?.accessToken

  if (!newAccessToken) {
    throw new Error('Refresh endpoint did not return a new access token')
  }

  tokenStorage.set(newAccessToken, undefined, persistent)
}

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiErrorBody>) => {
    const original = error.config as InternalAxiosRequestConfig & { _retried?: boolean }

    if (error.response?.status === 401 && original && !original._retried && tokenStorage.getAccess()) {
      original._retried = true
      try {
        refreshing ??= refreshSession().finally(() => (refreshing = null))
        await refreshing
        return http(original)
      } catch (err) {
        logger.warn('refreshSession failed', err)
        tokenStorage.clear()
        window.dispatchEvent(new CustomEvent('auth:logout'))
      }
    }

    throw new ApiError(error.response?.status ?? 0, error.response?.data)
  },
)
