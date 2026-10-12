import { env } from '@/config/env'
import { http } from '@/lib/api/http'
import { tokenStorage } from '@/lib/api/token-storage'
import type { LoginPayload, RegisterPayload, LoginResponse, AuthUser } from './auth.types'

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

function normalizeRoleValues(value: unknown): string[] {
  if (value == null) return []

  if (Array.isArray(value)) {
    return value.flatMap((item) => normalizeRoleValues(item))
  }

  if (typeof value === 'string') {
    return value
      .split(',')
      .map((part) => part.trim())
      .filter(Boolean)
  }

  if (typeof value === 'object') {
    const obj = value as Record<string, unknown>
    const candidates = [obj.name, obj.role, obj.value]
    return candidates.flatMap((candidate) => normalizeRoleValues(candidate))
  }

  return [String(value)]
}

function extractJwtRoleValues(payload: Record<string, any>): string[] {
  const keys = [
    'role',
    'roles',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role',
  ]

  const flattened = keys.flatMap((key) => normalizeRoleValues(payload?.[key]))
  return [...new Set(flattened.filter(Boolean))]
}

function buildUserFromToken(token?: string | null): AuthUser {
  const fallback: AuthUser = {
    id: 'user',
    name: 'User',
    email: '',
    role: 'Patient',
    roles: ['Patient'],
    activeRole: 'Patient',
  }

  if (!token) return fallback

  const payload = decodeJwtPayload(token)
  const roles = extractJwtRoleValues(payload)
  const primaryRole = (roles[0] ?? 'Patient') as any

  return {
    id: payload.sub ?? 'user',
    name: payload.name ?? payload.unique_name ?? payload.username ?? 'User',
    email: payload.email ?? '',
    role: primaryRole,
    roles: roles as any,
    activeRole: primaryRole,
  }
}

function buildLoginBody(payload: LoginPayload) {
  const username = (payload.username ?? payload.email ?? '').trim()
  return {
    username,
    password: payload.password,
  }
}

function buildRegisterBody(payload: RegisterPayload) {
  const username = (payload.username ?? payload.email ?? '').trim()
  const fullName = (payload.fullName ?? '').trim()
  const phoneNumber = (payload.phoneNumber ?? '').trim()
  const rawEmail = (payload.email ?? '').trim()
  const address = (payload.address ?? '').trim() || 'Chưa cập nhật'
  let dateOfBirthRaw: string | Date | null | undefined = payload.dateOfBirth ?? ''

  // Normalize dateOfBirth to yyyy-MM-dd (DateOnly expected by backend)
  let dateOfBirth: string | null = null
  if (dateOfBirthRaw) {
    // If it's already a string in yyyy-MM-dd, keep it. Otherwise try to parse.
    if (typeof dateOfBirthRaw === 'string') {
      // Some browsers may return yyyy-MM-dd; trim and use as-is if valid-looking
      const s = dateOfBirthRaw.trim()
      // Basic check: 4-2-2 digits
      if (/^\d{4}-\d{2}-\d{2}$/.test(s)) {
        dateOfBirth = s
      } else {
        const d = new Date(s)
        if (!Number.isNaN(d.getTime())) {
          const yyyy = d.getFullYear()
          const mm = String(d.getMonth() + 1).padStart(2, '0')
          const dd = String(d.getDate()).padStart(2, '0')
          dateOfBirth = `${yyyy}-${mm}-${dd}`
        } else {
          dateOfBirth = s // fallback: send as-is
        }
      }
    } else if (dateOfBirthRaw && Object.prototype.toString.call(dateOfBirthRaw) === '[object Date]') {
      const d = dateOfBirthRaw as Date
      const yyyy = d.getFullYear()
      const mm = String(d.getMonth() + 1).padStart(2, '0')
      const dd = String(d.getDate()).padStart(2, '0')
      dateOfBirth = `${yyyy}-${mm}-${dd}`
    } else {
      dateOfBirth = String(dateOfBirthRaw)
    }
  }

  const genderMap: Record<string, number> = {
    Male: 0,
    Female: 1,
    Other: 2,
  }

  const genderValue = typeof payload.gender === 'number' ? (payload.gender as number) : (genderMap[(payload as any).gender] ?? 0)

  const body: any = {
    username,
    password: payload.password,
    fullName,
    phoneNumber,
    email: rawEmail || null,
    gender: genderValue,
    address,
  }

  // Only attach dateOfBirth if we were able to produce a non-empty value
  if (dateOfBirth) body.dateOfBirth = dateOfBirth

  return body
}

function unwrapApiResult<T>(payload: any): T {
  if (!payload || typeof payload !== 'object') return payload as T
  if (payload.result !== undefined) return payload.result as T
  return payload as T
}

export const authApi = {
  login(payload: LoginPayload): Promise<LoginResponse> {
    return http.post<any>('/auth/login', buildLoginBody(payload)).then((r) => {
      const d = unwrapApiResult<any>(r.data)
      const accessToken = d?.accessToken ?? d?.token ?? ''
      const jwtUser = accessToken ? buildUserFromToken(accessToken) : {
        id: 'user',
        name: 'User',
        email: payload.username ?? payload.email ?? '',
        role: 'Patient',
        roles: ['Patient'],
        activeRole: 'Patient',
      } as AuthUser

      const backendUser = (d?.user ?? {}) as Partial<AuthUser>
      const explicitRoleCandidates = [
        backendUser.role,
        backendUser.roles,
        d?.role,
        d?.roles,
        backendUser.activeRole,
      ]

      const roleFromBody = explicitRoleCandidates.flatMap((item) => normalizeRoleValues(item)).find(Boolean) ?? jwtUser.role
      const rolesFromBody = explicitRoleCandidates
        .flatMap((item) => normalizeRoleValues(item))
        .filter(Boolean)
      const resolvedRoles = rolesFromBody.length > 0 ? [...new Set(rolesFromBody)] : (jwtUser.roles ?? ['Patient'])
      const resolvedRole = (roleFromBody ?? resolvedRoles[0] ?? jwtUser.role ?? 'Patient') as AuthUser['role']

      const mergedUser: AuthUser = {
        ...jwtUser,
        ...backendUser,
        id: backendUser.id ?? jwtUser.id,
        name: backendUser.name ?? jwtUser.name,
        email: backendUser.email ?? jwtUser.email ?? payload.username ?? payload.email ?? '',
        role: resolvedRole,
        roles: resolvedRoles as AuthUser['roles'],
        activeRole: (backendUser.activeRole ?? resolvedRole ?? jwtUser.activeRole ?? 'Patient') as AuthUser['activeRole'],
      }

      return {
        accessToken,
        refreshToken: d?.refreshToken,
        role: (mergedUser.role ?? 'Patient') as LoginResponse['role'],
        roles: (mergedUser.roles ?? [mergedUser.role ?? 'Patient']) as LoginResponse['roles'],
        user: mergedUser,
      } as LoginResponse
    })
  },

  register(payload: RegisterPayload): Promise<LoginResponse> {
    const body = buildRegisterBody(payload)
    // Helpful debug: show exactly what will be sent to the backend
    console.debug('[authApi] register payload', body)

    return http
      .post<any>('/auth/register', body)
      .then((r) => {
        const d = unwrapApiResult<any>(r.data)
        if (d && d.token && !d.accessToken) d.accessToken = d.token
        const accessToken = d?.accessToken ?? d?.token ?? ''
        const fallbackUser = {
          id: 'user',
          name: payload.fullName,
          email: payload.email,
          role: 'Patient',
          roles: ['Patient'],
          activeRole: 'Patient',
        } as AuthUser
        return {
          accessToken,
          refreshToken: d?.refreshToken,
          user: d?.user ?? fallbackUser,
        } as LoginResponse
      })
      .catch((err) => {
        // Log error details to help debugging in DevTools
        try {
          console.debug('[authApi] register error', err)
          // If axios/ApiError with structured body, attempt to log server payload
          if (err?.response?.data) console.debug('[authApi] server response body', err.response.data)
        } catch (loggingErr) {
          console.warn('Failed to log register error', loggingErr)
        }
        throw err
      })
  },

  async getMe(): Promise<AuthUser> {
    try {
      const response = await http.get<AuthUser>('/auth/me')
      return response.data
    } catch (error: any) {
      const token = tokenStorage.getAccess()
      const looksLikeJwt = typeof token === 'string' && token.split('.').length === 3 && !!token.split('.')[1]

      if (token && looksLikeJwt) {
        return buildUserFromToken(token)
      }

      if (error?.response?.status === 404 || error?.response?.status === 403) {
        return buildUserFromToken(token)
      }

      throw error
    }
  },


  forgotPassword(phoneNumberOrEmail: string): Promise<{ message: string }> {
    if (env.enableMock) {
      return Promise.resolve({
        message: 'Nếu số điện thoại hoặc email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi.',
      })
    }

    return http
      .post<{ message: string }>('/forgot-password', {
        phoneNumberOrEmail: phoneNumberOrEmail.trim(),
      })
      .then((r) => r.data)
  },

  changePassword(payload: { currentPassword: string; newPassword: string }): Promise<{ message: string }> {
    if (env.enableMock) {
      return Promise.resolve({ message: 'Mật khẩu đã được cập nhật thành công.' })
    }

    return http
      .post<{ message: string }>('/change-password', {
        currentPassword: payload.currentPassword,
        newPassword: payload.newPassword,
      })
      .then((r) => r.data)
  },

  mockMultiRoleLogin(user: Partial<AuthUser> = {}): LoginResponse {
    return {
      accessToken: 'mock-access-token',
      refreshToken: 'mock-refresh-token',
      user: {
        id: 1,
        name: 'Demo User',
        email: 'demo@clinic.com',
        role: 'Patient',
        roles: ['Patient', 'Doctor'],
        activeRole: 'Patient',
        ...user,
      },
    }
  },
}
