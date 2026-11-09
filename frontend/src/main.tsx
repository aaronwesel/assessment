/**
 * Vite + React SPA bootstrap.
 * Mounts `<App />` which wraps routes in AuthProvider for JWT session state.
 */
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
