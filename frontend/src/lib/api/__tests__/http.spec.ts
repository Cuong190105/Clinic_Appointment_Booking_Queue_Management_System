import { beforeEach, describe, expect, it, vi } from 'vitest'
import axios from 'axios'
import { ApiError, http } from '../http'
import { tokenStorage } from '../token-storage'

describe('ApiError', () => {
  it('prefers errorMessages array from ApiResponse envelope', () => {
    const err = new ApiError(400, {
      errorMessages: ['Họ và tên không hợp lệ', 'Số điện thoại không đúng định dạng'],
      message: 'Fallback message',
    })
    expect(err.status).toBe(400)
    expect(err.message).toBe('Họ và tên không hợp lệ | Số điện thoại không đúng định dạng')
  })

  it('uses body.message when errorMessages is absent or empty', () => {
    const err = new ApiError(404, {
      message: 'Không tìm thấy thông tin bệnh nhân',
    })
    expect(err.status).toBe(404)
    expect(err.message).toBe('Không tìm thấy thông tin bệnh nhân')
  })

  it('extracts structured fieldErrors from body.errors', () => {
    const err = new ApiError(422, {
      errors: {
        Email: ['Email đã tồn tại'],
        PhoneNumber: ['Số điện thoại không hợp lệ'],
      },
    })
    expect(err.fieldErrors).toBeDefined()
    expect(err.fieldErrors?.['Email']).toEqual(['Email đã tồn tại'])
  })

  it('extracts structured fieldErrors from body.result.errors', () => {
    const err = new ApiError(400, {
      result: {
        errors: {
          Password: ['Mật khẩu phải chứa ít nhất 8 ký tự'],
        },
      },
    })
    expect(err.fieldErrors?.['Password']).toEqual(['Mật khẩu phải chứa ít nhất 8 ký tự'])
  })

  it('fallbacks to default message when body is empty', () => {
    const err = new ApiError(500)
    expect(err.status).toBe(500)
    expect(err.message).toBe('Request failed with status 500')
  })
})

describe('http client interceptors', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    sessionStorage.clear()
    tokenStorage.clear()
  })

  it('automatically attaches Bearer token and X-Correlation-ID to outgoing requests', async () => {
    tokenStorage.set('valid-access-token', 'valid-refresh-token', false)

    let capturedHeaders: any = null
    const adapter = vi.fn().mockImplementation((config: any) => {
      capturedHeaders = config.headers
      return Promise.resolve({
        data: { ok: true },
        status: 200,
        statusText: 'OK',
        headers: {},
        config,
      })
    })

    const customHttp = axios.create({ adapter })
    const requestHandler = (http.interceptors.request as any).handlers[0]
    customHttp.interceptors.request.use(requestHandler.fulfilled)

    await customHttp.get('/test-endpoint')

    expect(capturedHeaders['Authorization']).toBe('Bearer valid-access-token')
    expect(capturedHeaders['X-Correlation-ID']).toBeDefined()
    expect(typeof capturedHeaders['X-Correlation-ID']).toBe('string')
  })

  it('re-uses existing X-Correlation-ID if provided by caller', async () => {
    let capturedHeaders: any = null
    const adapter = vi.fn().mockImplementation((config: any) => {
      capturedHeaders = config.headers
      return Promise.resolve({
        data: { ok: true },
        status: 200,
        statusText: 'OK',
        headers: {},
        config,
      })
    })

    const customHttp = axios.create({ adapter })
    const requestHandler = (http.interceptors.request as any).handlers[0]
    customHttp.interceptors.request.use(requestHandler.fulfilled)

    await customHttp.get('/test-endpoint', {
      headers: { 'X-Correlation-ID': 'custom-cid-12345' },
    })

    expect(capturedHeaders['X-Correlation-ID']).toBe('custom-cid-12345')
  })

  it('on 401 with active token, triggers refreshSession and replays original request', async () => {
    tokenStorage.set('expired-access-token', 'my-refresh-token', false)

    let callsCount = 0
    const adapter = vi.fn().mockImplementation((config: any) => {
      callsCount++
      if (callsCount === 1) {
        return Promise.reject({
          response: { status: 401, data: { message: 'Token expired' } },
          config,
        })
      }
      return Promise.resolve({
        data: { success: true },
        status: 200,
        statusText: 'OK',
        headers: {},
        config,
      })
    })

    vi.spyOn(axios, 'post').mockResolvedValueOnce({
      data: {
        accessToken: 'new-refreshed-access-token',
        refreshToken: 'new-refreshed-refresh-token',
      },
    })

    const testClient = axios.create({ adapter })
    const responseHandler = (http.interceptors.response as any).handlers[0]
    testClient.interceptors.response.use(
      responseHandler.fulfilled,
      responseHandler.rejected,
    )

    const response = await testClient.get('/protected-resource')

    expect(response.data).toEqual({ success: true })
    expect(axios.post).toHaveBeenCalledWith(
      expect.stringContaining('/auth/refresh'),
      { refreshToken: 'my-refresh-token' },
      expect.any(Object),
    )
    expect(tokenStorage.getAccess()).toBe('new-refreshed-access-token')
  })

  it('on failed refreshSession, clears token storage and dispatches auth:logout event', async () => {
    tokenStorage.set('bad-token', 'bad-refresh', false)

    const adapter = vi.fn().mockImplementation((config: any) => {
      return Promise.reject({
        response: { status: 401, data: { message: 'Unauthorized' } },
        config,
      })
    })

    vi.spyOn(axios, 'post').mockRejectedValueOnce({
      response: { status: 401, data: { message: 'Invalid refresh token' } },
    })

    const logoutEventSpy = vi.fn()
    window.addEventListener('auth:logout', logoutEventSpy)

    const testClient = axios.create({ adapter })
    const responseHandler = (http.interceptors.response as any).handlers[0]
    testClient.interceptors.response.use(
      responseHandler.fulfilled,
      responseHandler.rejected,
    )

    await expect(testClient.get('/fail-endpoint')).rejects.toThrow(ApiError)

    expect(tokenStorage.getAccess()).toBeNull()
    expect(tokenStorage.getRefresh()).toBeNull()
    expect(logoutEventSpy).toHaveBeenCalledTimes(1)

    window.removeEventListener('auth:logout', logoutEventSpy)
  })
})
