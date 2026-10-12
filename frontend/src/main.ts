import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import { i18n } from './lib/i18n'
import { logger } from '@/lib/logger'

const app = createApp(App) as any

;(window as any).__clinicAuthDebug = () => {
  const token = sessionStorage.getItem('auth.accessToken') ?? localStorage.getItem('auth.accessToken')
  if (!token) {
    return { accessToken: null, payload: null, roles: [] }
  }

  const base64 = token.split('.')[1]
  if (!base64) {
    return { accessToken: token, payload: null, roles: [] }
  }

  const normalized = base64.replace(/-/g, '+').replace(/_/g, '/')
  const padded = normalized + '='.repeat((4 - (normalized.length % 4)) % 4)
  const payload = JSON.parse(atob(padded))
  const roleKeys = [
    'role',
    'roles',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role',
  ]
  const roles = roleKeys.flatMap((key) => {
    const value = payload[key]
    if (value == null) return []
    if (Array.isArray(value)) return value.map(String)
    if (typeof value === 'string') return value.split(',').map((part) => part.trim()).filter(Boolean)
    return [String(value)]
  })

  return {
    accessToken: token,
    payload,
    roles: [...new Set(roles)],
  }
}

// Install Pinia first so stores can be used for hydration
(app as any).use(createPinia())

// Before registering router, hydrate auth if we have a token to avoid router redirect races.
;(async () => {
  try {
    const tokenMod = await import('@/lib/api/token-storage')
    const hasAccessToken = !!tokenMod.tokenStorage.getAccess()

    if (hasAccessToken) {
      const mod = await import('@/stores/auth')
      const auth = mod.useAuthStore()
      // prefer hydrate helper that sets the store.hydrating flag
      if (typeof auth.hydrate === 'function') {
        await auth.hydrate()
      } else if (!auth.isAuthenticated) {
        await auth.fetchMe()
      }
    }
  } catch (e) {
    // fetching profile failed; ensure app reacts to cleared session
    logger.warn('Failed to hydrate auth profile on startup', e)
  } finally {
    // Now register router and i18n and mount the app
    ;(app as any).use(router)
    ;(app as any).use(i18n)
    app.mount('#app')
  }
})()

// React to forced logout from the HTTP layer (refresh failed).
window.addEventListener('auth:logout', () => {
  // router will be registered by the time this runs
  router.push('/login')
})
