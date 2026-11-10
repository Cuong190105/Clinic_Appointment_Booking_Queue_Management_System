export const env = {
  appName: import.meta.env.VITE_APP_NAME ?? 'Clinic',
  // Both Vite dev and the nginx container expose the backend through /api.
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '/api',
  defaultLocale: import.meta.env.VITE_DEFAULT_LOCALE ?? 'vi',
  // Bật mock mode khi chưa có backend thực: VITE_ENABLE_MOCK=true
  enableMock: import.meta.env.VITE_ENABLE_MOCK === 'true',
  isDev: import.meta.env.DEV,
  isProd: import.meta.env.PROD,
} as const

export type AppEnv = typeof env
