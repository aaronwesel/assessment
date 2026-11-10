/**
 * Top-level router shell.
 *
 * Public: landing `/`, login, register, crawl console (`/crawl*`).
 * Authenticated: `/app` home, `/billing` settings.
 * Admin/superadmin: `/superadmin/*` console.
 */
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { Nav } from './components/Nav'
import { RequireAuth } from './components/RequireAuth'
import { RequireRole } from './components/RequireRole'
import { BillingSettingsPage } from './pages/billing/BillingSettingsPage'
import { HistoryPage } from './pages/crawl/HistoryPage'
import { JobDetailsPage } from './pages/crawl/JobDetailsPage'
import { StartCrawlPage } from './pages/crawl/StartCrawlPage'
import { HomePage } from './pages/HomePage'
import { LandingPage } from './pages/LandingPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { SuperadminAuditPage } from './pages/superadmin/AuditPage'
import { SuperadminBillingPage } from './pages/superadmin/BillingPage'
import { SuperadminBreakGlassPage } from './pages/superadmin/BreakGlassPage'
import { SuperadminFlagsPage } from './pages/superadmin/FlagsPage'
import { SuperadminHealthPage } from './pages/superadmin/HealthPage'
import { SuperadminOverviewPage } from './pages/superadmin/OverviewPage'
import { SuperadminLayout } from './pages/superadmin/SuperadminLayout'
import { SuperadminWebhooksPage } from './pages/superadmin/WebhooksPage'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <div className="app-shell">
          <Nav />
          <Routes>
            <Route path="/" element={<LandingPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/crawl" element={<StartCrawlPage />} />
            <Route path="/crawl/jobs/:jobId" element={<JobDetailsPage />} />
            <Route path="/crawl/history" element={<HistoryPage />} />
            <Route
              path="/app"
              element={
                <RequireAuth>
                  <HomePage />
                </RequireAuth>
              }
            />
            <Route
              path="/billing"
              element={
                <RequireAuth>
                  <BillingSettingsPage />
                </RequireAuth>
              }
            />
            <Route
              path="/superadmin"
              element={
                <RequireRole>
                  <SuperadminLayout />
                </RequireRole>
              }
            >
              <Route index element={<SuperadminOverviewPage />} />
              <Route path="health" element={<SuperadminHealthPage />} />
              <Route path="flags" element={<SuperadminFlagsPage />} />
              <Route path="audit" element={<SuperadminAuditPage />} />
              <Route path="break-glass" element={<SuperadminBreakGlassPage />} />
              <Route path="webhooks" element={<SuperadminWebhooksPage />} />
              <Route path="billing" element={<SuperadminBillingPage />} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </div>
      </BrowserRouter>
    </AuthProvider>
  )
}
