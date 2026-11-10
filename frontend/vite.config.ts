/**
 * Vite config for the React + TypeScript SPA.
 * Dev server proxies are intentionally omitted — VITE_API_BASE_URL points at
 * the FastAPI process (Compose or local uvicorn) with CORS allow-listed.
 */
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
})
