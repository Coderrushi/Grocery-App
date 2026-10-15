import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// The browser only ever talks to the Vite origin. In development, /api is proxied to ASP.NET Core,
// so the app and API share one origin — the same shape as production (reverse proxy), which lets
// auth use secure HttpOnly cookies without CORS.
const apiTarget = process.env.API_URL ?? 'http://localhost:5230'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: false },
      '/media': { target: apiTarget, changeOrigin: false }, // uploaded product images
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.js'],
    css: false,
  },
})
