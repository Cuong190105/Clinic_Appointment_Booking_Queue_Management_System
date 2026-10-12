import { ApiError } from './http'

export function getFieldErrors(e: unknown): Record<string, string[]> | undefined {
  if (!e) return undefined

  // ApiError instance containing fieldErrors from backend
  if (e instanceof ApiError && e.fieldErrors) return e.fieldErrors

  const anyErr = e as any
  // axios-like error with response.data.errors or response.data.result.errors
  if (anyErr?.response?.data?.errors && typeof anyErr.response.data.errors === 'object') {
    return anyErr.response.data.errors as Record<string, string[]>
  }

  if (anyErr?.response?.data?.result?.errors && typeof anyErr.response.data.result.errors === 'object') {
    return anyErr.response.data.result.errors as Record<string, string[]>
  }

  // Backend ApiResponse envelope may include errorMessages (array of strings) but no field map - cannot map to fields reliably
  // Return undefined in that case so callers can display the global message from authStore.error
  return undefined
}
