/**
 * Capture auth UI screenshots for ASS-42/43 (Playwright).
 */
import { chromium } from 'playwright'
import fs from 'node:fs'
import path from 'node:path'

const outDir = process.argv[2]
if (!outDir) {
  console.error('Usage: node auth-screenshots.mjs <output-dir>')
  process.exit(1)
}
fs.mkdirSync(outDir, { recursive: true })

const base = 'http://127.0.0.1:4173'
const api = 'http://127.0.0.1:8000'
const password = 'SecurePass1!x'

async function seedSession(page, email) {
  const reg = await fetch(`${api}/api/v1/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, full_name: 'Screenshot User' }),
  })
  if (reg.status === 409) {
    const login = await fetch(`${api}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    })
    if (!login.ok) throw new Error('login failed for screenshot seed')
    var tokens = await login.json()
  } else {
    if (!reg.ok) throw new Error(`register failed ${reg.status}`)
    var tokens = await reg.json()
  }
  const me = await (
    await fetch(`${api}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${tokens.access_token}` },
    })
  ).json()
  await page.evaluate(
    ({ tokens, user }) => {
      sessionStorage.setItem('assessment.auth.tokens', JSON.stringify(tokens))
      sessionStorage.setItem(
        'assessment.auth.user',
        JSON.stringify({
          id: user.subject,
          email: user.email,
          name: user.full_name ?? user.email,
          roles: user.roles,
          provider: 'password',
          mfaEnabled: user.mfa_enabled,
          mfaPolicyRequired: user.mfa_policy_required,
        }),
      )
    },
    { tokens, user: me },
  )
}

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } })

await page.goto(`${base}/register`, { waitUntil: 'networkidle' })
await page.screenshot({ path: path.join(outDir, '01-register-onetap.png'), fullPage: true })

await page.goto(`${base}/login`, { waitUntil: 'networkidle' })
await page.screenshot({ path: path.join(outDir, '02-login-onetap.png'), fullPage: true })

await seedSession(page, 'screenshot-std@example.com')
await page.goto(`${base}/app`, { waitUntil: 'networkidle' })
await page.screenshot({ path: path.join(outDir, '03-home-optional-mfa.png'), fullPage: true })

await page.getByRole('button', { name: 'Enroll', exact: true }).click()
await page.locator('img.mfa-qr').waitFor({ state: 'visible', timeout: 15000 })
await page.screenshot({ path: path.join(outDir, '04-mfa-enroll-qr.png'), fullPage: true })

await browser.close()
console.log('screenshots saved to', outDir)
