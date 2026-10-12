import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), vueDevTools(), tailwindcss()],

  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },

  server: {
    host: 'localhost',
    port: 5173,
    strictPort: true,
    proxy: {
      '/auth': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/api': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/appointments': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/doctors': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/slots': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/queue': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/me': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/medical-records': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/patients': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/users': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/shifts': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/specialties': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/employees': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
      '/receptionist': {
        target: 'https://localhost:61658',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
