export const logger = {
  debug: (...args: any[]) => {
    if (import.meta.env.DEV) console.debug('[app]', ...args)
  },
  info: (...args: any[]) => console.info('[app]', ...args),
  warn: (...args: any[]) => console.warn('[app]', ...args),
  error: (...args: any[]) => console.error('[app]', ...args),
}
