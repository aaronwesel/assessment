/**
 * Public marketing landing — brand-first hero, single CTA group.
 * Authenticated visitors can continue to the app; others register or sign in.
 */
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function LandingPage() {
  const { isAuthenticated } = useAuth()

  return (
    <main className="landing">
      <section className="landing-hero" aria-labelledby="landing-brand">
        <div className="landing-atmosphere" aria-hidden="true">
          <div className="landing-orb landing-orb-a" />
          <div className="landing-orb landing-orb-b" />
          <div className="landing-grid" />
        </div>

        <div className="landing-hero-copy">
          <p className="landing-kicker">HIPAA-minded SaaS scaffold</p>
          <h1 id="landing-brand" className="landing-brand">
            Assessment
          </h1>
          <p className="landing-tagline">
            Secure sessions, team roles, and ops-ready controls — so the live
            prompt becomes product, not plumbing.
          </p>
          <div className="landing-cta">
            {isAuthenticated ? (
              <Link className="btn btn-primary landing-btn" to="/app">
                Open app
              </Link>
            ) : (
              <>
                <Link className="btn btn-primary landing-btn" to="/register">
                  Create account
                </Link>
                <Link className="btn btn-ghost landing-btn" to="/login">
                  Sign in
                </Link>
              </>
            )}
          </div>
        </div>

        <div className="landing-visual" aria-hidden="true">
          <div className="landing-panel">
            <span className="landing-panel-label">Platform</span>
            <ul className="landing-panel-list">
              <li>JWT + MFA</li>
              <li>Teams RBAC</li>
              <li>Health / SLO</li>
              <li>Audit-ready</li>
            </ul>
          </div>
        </div>
      </section>
    </main>
  )
}
