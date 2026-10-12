export const env = {
  appName: import.meta.env.VITE_APP_NAME ?? 'Clinic',
  // Trong dev, dùng cùng origin với Vite để tránh CORS.
  // Vite proxy sẽ forward /auth/* tới backend https://localhost:61658.
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
  defaultLocale: import.meta.env.VITE_DEFAULT_LOCALE ?? 'vi',
  // Bật mock mode khi chưa có backend thực: VITE_ENABLE_MOCK=true
  enableMock: import.meta.env.VITE_ENABLE_MOCK === 'true',
  isDev: import.meta.env.DEV,
  isProd: import.meta.env.PROD,
} as const

export type AppEnv = typeof env
