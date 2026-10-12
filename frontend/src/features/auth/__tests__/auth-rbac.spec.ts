import { createRouter, createMemoryHistory } from 'vue-router'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const { routerPush } = vi.hoisted(() => ({
  routerPush: vi.fn(),
}))

vi.mock('vue-router', async () => {
  const actual = await vi.importActual<typeof import('vue-router')>('vue-router')
  return {
    ...actual,
    useRouter: vi.fn(() => ({ push: routerPush })),
  }
})

import { useAuthStore } from '@/stores/auth'
import { authApi } from '@/features/auth/auth.api'
import { http } from '@/lib/api/http'
import { tokenStorage } from '@/lib/api/token-storage'
import SelectRoleView from '@/views/auth/SelectRoleView.vue'
import type { UserRole } from '@/features/auth/auth.types'

describe('auth complete flow', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.clear()
    tokenStorage.clear()
    routerPush.mockClear()
  })

  it('prefers backend user metadata and role claims returned by the login response', async () => {
    vi.spyOn(http, 'post').mockResolvedValue({
      data: {
        accessToken: 'abc',
        refreshToken: 'refresh-token-456',
        role: 'Admin',
        roles: ['Admin'],
        user: {
          id: 42,
          name: 'Nguyễn Văn A',
          email: 'a@example.com',
          role: 'Admin',
          roles: ['Admin'],
          activeRole: 'Admin',
        },
      },
    } as any)

    const result = await authApi.login({
      email: 'a@example.com',
      password: 'Password123!',
    })

    expect(result.role).toBe('Admin')
    expect(result.roles).toEqual(['Admin'])
    expect(result.user.role).toBe('Admin')
    expect(result.user.roles).toEqual(['Admin'])
  })

  it('login success stores user and tokens', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({
      accessToken: 'access-token-123',
      refreshToken: 'refresh-token-456',
      user: {
        id: 42,
        name: 'Nguyễn Văn A',
        email: 'a@example.com',
        role: 'Patient',
        roles: ['Patient'],
        activeRole: 'Patient',
      },
    })

    const auth = useAuthStore()
    const user = await auth.login({
      email: 'a@example.com',
      password: 'Password123!',
      role: 'Patient',
    })

    expect(user.role).toBe('Patient')
    expect(auth.user?.roles).toEqual(['Patient'])
    expect(auth.user?.email).toBe('a@example.com')
    expect(auth.isAuthenticated).toBe(true)
    expect(tokenStorage.getAccess()).toBe('access-token-123')
    expect(tokenStorage.getRefresh()).toBe('refresh-token-456')
    expect(sessionStorage.getItem('auth.user')).toContain('Nguyễn Văn A')
    expect(localStorage.getItem('auth.user')).toBeNull()
  })

  it('login failure keeps auth clean and stores error', async () => {
    vi.spyOn(authApi, 'login').mockRejectedValue({
      response: {
        data: { message: 'Email hoặc mật khẩu không đúng' },
      },
    })

    const auth = useAuthStore()

    await expect(
      auth.login({
        email: 'wrong@example.com',
        password: 'badpass',
        role: 'Patient',
      }),
    ).rejects.toBeDefined()

    expect(auth.isAuthenticated).toBe(false)
    expect(auth.user).toBeNull()
    expect(auth.error).toBe('Email hoặc mật khẩu không đúng')
    expect(tokenStorage.getAccess()).toBeNull()
  })

  it('register success stores auth state and tokens', async () => {
    vi.spyOn(authApi, 'register').mockResolvedValue({
      accessToken: 'register-access-token',
      refreshToken: 'register-refresh-token',
      user: {
        id: 11,
        name: 'Ms. Lan',
        email: 'lan@example.com',
        role: 'Patient',
        roles: ['Patient'],
        activeRole: 'Patient',
      },
    })

    const auth = useAuthStore()
    const result = await auth.register({
      fullName: 'Ms. Lan',
      phoneNumber: '0912345678',
      email: 'lan@example.com',
      password: 'Password123!',
      gender: 'Female',
      dateOfBirth: '1998-05-10',
    })

    expect(result.user.role).toBe('Patient')
    expect(auth.isAuthenticated).toBe(true)
    expect(tokenStorage.getAccess()).toBe('register-access-token')
    expect(tokenStorage.getRefresh()).toBe('register-refresh-token')
    expect(auth.user?.name).toBe('Ms. Lan')
  })

  it('register failure keeps state empty and exposes backend message', async () => {
    vi.spyOn(authApi, 'register').mockRejectedValue({
      response: {
        data: { message: 'Email đã tồn tại' },
      },
    })

    const auth = useAuthStore()

    await expect(
      auth.register({
        fullName: 'Ms. Lan',
        phoneNumber: '0912345678',
        email: 'lan@example.com',
        password: 'Password123!',
        gender: 'Female',
        dateOfBirth: '1998-05-10',
      }),
    ).rejects.toBeDefined()

    expect(auth.isAuthenticated).toBe(false)
    expect(auth.user).toBeNull()
    expect(auth.error).toBe('Email đã tồn tại')
    expect(tokenStorage.getAccess()).toBeNull()
  })

  it('rehydrates user from localStorage on app startup only when a valid session exists', () => {
    const savedUser = {
      id: 99,
      name: 'Người dùng lưu lại',
      email: 'persist@example.com',
      role: 'Patient' as const,
      roles: ['Patient'] as UserRole[],
      activeRole: 'Patient' as const,
    }
    tokenStorage.set('valid-access-token', 'valid-refresh-token', true)
    localStorage.setItem('auth.user', JSON.stringify(savedUser))

    const auth = useAuthStore()

    expect(auth.user?.name).toBe('Người dùng lưu lại')
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.currentUserRole).toBe('Patient')
    expect(auth.user?.roles).toEqual(['Patient'])
  })

  it('clears stale persisted data when switching from remember-me to session-only mode', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({
      accessToken: 'session-switch-token',
      refreshToken: 'session-switch-refresh',
      user: {
        id: 7,
        name: 'Admin session',
        email: 'admin@clinic.com',
        role: 'Admin',
        roles: ['Admin'],
        activeRole: 'Admin',
      },
    })

    const auth = useAuthStore()
    await auth.login({ email: 'admin@clinic.com', password: 'Password123!', rememberMe: true })

    expect(localStorage.getItem('auth.user')).not.toBeNull()

    await auth.login({ email: 'admin@clinic.com', password: 'Password123!', rememberMe: false })

    expect(localStorage.getItem('auth.user')).toBeNull()
    expect(sessionStorage.getItem('auth.user')).toContain('Admin session')
    expect(tokenStorage.getAccess()).toBe('session-switch-token')
  })

  it('supports login with multiple roles and prompts the user to choose an active role', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({
      accessToken: 'multi-role-token',
      refreshToken: 'multi-role-refresh',
      user: {
        id: 55,
        name: 'BS. Lan',
        email: 'multi@example.com',
        role: 'Doctor',
        roles: ['Patient', 'Doctor'],
        activeRole: 'Doctor',
      },
    })

    const auth = useAuthStore()
    const user = await auth.login({
      email: 'multi@example.com',
      password: 'Password123!',
    })

    expect(user.roles).toEqual(['Patient', 'Doctor'])
    expect(user.activeRole).toBe('Doctor')
    expect(auth.hasRole(['Patient', 'Doctor'])).toBe(true)
    expect(auth.currentUserRole).toBe('Doctor')
  })

  it('select role screen stores the active role and redirects to the patient dashboard', async () => {
    routerPush.mockClear()

    const wrapper = mount(SelectRoleView)
    const roleCard = wrapper.findAll('.group')[0]

    expect(roleCard?.exists()).toBe(true)
    await roleCard!.trigger('click')

    expect(routerPush).toHaveBeenCalledWith('/patient')
  })

  it('redirects unauthenticated users to select-role and preserves redirect path', async () => {
    const routes = [
      { path: '/select-role', name: 'select-role', component: { template: '<div>Select role</div>' }, meta: { public: true } },
      { path: '/', name: 'home', component: { template: '<div>Home</div>' } },
      { path: '/patient', name: 'patient-home', component: { template: '<div>Patient</div>' }, meta: { roles: ['Patient'] as UserRole[] } },
    ]

    const router = createRouter({
      history: createMemoryHistory(),
      routes,
    })

    router.beforeEach((to) => {
      const auth = useAuthStore()
      if (to.meta.public) return true
      if (!auth.isAuthenticated) {
        return { name: 'select-role', query: { redirect: to.fullPath } }
      }
      const requiredRoles = to.meta.roles as UserRole[] | undefined
      if (requiredRoles && requiredRoles.length > 0 && !auth.hasRole(requiredRoles)) {
        return { name: 'home' }
      }
      return true
    })

    await router.push('/patient')
    expect(router.currentRoute.value.name).toBe('select-role')
    expect(router.currentRoute.value.query.redirect).toBe('/patient')
  })

  it('allows authorized users and blocks unauthorized users by role', async () => {
    const routes = [
      { path: '/select-role', name: 'select-role', component: { template: '<div>Select role</div>' }, meta: { public: true } },
      { path: '/', name: 'home', component: { template: '<div>Home</div>' } },
      { path: '/patient', name: 'patient-home', component: { template: '<div>Patient</div>' }, meta: { roles: ['Patient'] as UserRole[] } },
      { path: '/doctor', name: 'doctor-home', component: { template: '<div>Doctor</div>' }, meta: { roles: ['Doctor'] as UserRole[] } },
    ]

    const router = createRouter({
      history: createMemoryHistory(),
      routes,
    })

    router.beforeEach((to) => {
      const auth = useAuthStore()
      if (to.meta.public) return true
      if (!auth.isAuthenticated) {
        return { name: 'select-role', query: { redirect: to.fullPath } }
      }
      const requiredRoles = to.meta.roles as UserRole[] | undefined
      if (requiredRoles && requiredRoles.length > 0 && !auth.hasRole(requiredRoles)) {
        return { name: 'home' }
      }
      return true
    })

    const auth = useAuthStore()
    auth.setUser({
      id: 1,
      name: 'Người bệnh',
      email: 'patient@example.com',
      role: 'Patient',
    })

    await router.push('/patient')
    expect(router.currentRoute.value.name).toBe('patient-home')

    await router.push('/doctor')
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('logout clears local storage and resets auth state', () => {
    const auth = useAuthStore()
    auth.setUser({
      id: 5,
      name: 'Bác sĩ Hoàng',
      email: 'hoang@example.com',
      role: 'Doctor',
      roles: ['Doctor'],
      activeRole: 'Doctor',
    })
    auth.setToken('token-a', 'token-r')

    auth.logout()

    expect(auth.isAuthenticated).toBe(false)
    expect(auth.user).toBeNull()
    expect(tokenStorage.getAccess()).toBeNull()
    expect(tokenStorage.getRefresh()).toBeNull()
    expect(localStorage.getItem('auth.user')).toBeNull()
  })
})
