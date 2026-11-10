/**
 * Vite config for the React + TypeScript SPA.
 * API traffic uses VITE_API_BASE_URL (CORS allow-listed). Doc paths proxy locally
 * so landing/nav links to /swagger and /docs match nginx in Compose.
 */
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const crawlApiTarget = process.env.VITE_CRAWL_API_PROXY_TARGET ?? 'http://127.0.0.1:8080'
const platformApiTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://127.0.0.1:8000'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/swagger': { target: crawlApiTarget, changeOrigin: true },
      '/docs': { target: platformApiTarget, changeOrigin: true },
      '/openapi.json': { target: platformApiTarget, changeOrigin: true },
    },
  },
})
