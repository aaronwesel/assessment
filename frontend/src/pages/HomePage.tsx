import { useAuth } from '../auth/AuthContext'

/** Authenticated empty home — no domain screens yet. */
export function HomePage() {
  const { user } = useAuth()

  return (
    <main className="page home-page">
      <h1>Home</h1>
      <p className="lede">
        Signed in as {user?.email}. Product screens land after the assessment
        prompt.
      </p>
    </main>
  )
}
