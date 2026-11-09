# Security checklist (HIPAA-ready prep)

**Not a certification.** Engineering controls for assessment-day SaaS prep. Pair with `docs/hipaa-checklist.md`.

## Passwords

| Control | Status |
|---------|--------|
| bcrypt hashing (`app.core.security`) | Done — Argon2id optional later upgrade |
| Complexity: ≥12 chars, upper/lower/digit/special | Done (`app.core.passwords`) |
| Local common-password denylist | Done (lightweight stub; prefer HIBP k-anonymity in prod) |
| Rate-limit failed logins (Redis) | Done |
| Lockout / backoff stub after N failures | Done (`app.core.rate_limit`) |
| Never log passwords / tokens | Done (`app.core.logging` redaction) |

## MFA

| Control | Status |
|---------|--------|
| TOTP enroll (`POST /api/v1/auth/mfa/enroll`) | Done (pyotp) |
| TOTP confirm (`POST /api/v1/auth/mfa/confirm`) | Done — enables `mfa_enabled` |
| Verify TOTP / backup (`POST /api/v1/auth/mfa/verify`) | Done |
| Login MFA gate when `mfa_enabled` | Done (`totp_code` or `backup_code`) |
| Backup codes bcrypt-hashed; plaintext once | Done |
| TOTP secret Fernet-encrypted at rest | Done |
| Frontend enroll/verify UI stubs | Done (`MfaPanel`) |
| Recovery: backup codes; disable with proof | Done (`POST /auth/mfa/disable`) |

## Encryption

| Control | Status |
|---------|--------|
| TLS in transit (ALB HTTPS, Redis transit encryption) | Terraform scaffold — TLS-only outside local Compose |
| Encryption at rest: RDS/Aurora + Redis + CloudWatch via KMS CMK | Terraform scaffold (comments strengthened) |
| S3 / tf-state: `encrypt = true` when remote backend enabled | Documented in `versions.tf` + this checklist |
| App-level Fernet for MFA secrets | Done (`FIELD_ENCRYPTION_KEY` / derive from `SECRET_KEY` locally) |
| Bulk PHI columns | Prefer DB TDE + KMS; do not invent ad-hoc AES for PHI blobs |

### TLS-only notes

- Production: terminate TLS on ALB (`ELBSecurityPolicy-TLS13-1-2-2021-06`); redirect HTTP→HTTPS on assessment day.
- Redis: `transit_encryption_enabled = true` + AUTH token.
- App→DB: require SSL in non-local `DATABASE_URL` (`sslmode=require`).
- Local Compose may use plain HTTP/TCP; never for PHI workloads.

### Cookie / JWT settings

- Current SPA uses Bearer JWTs in `Authorization` (JSON body issue); tokens held in `sessionStorage`.
- If cookies are added: `HttpOnly`, `Secure` (non-local), `SameSite=Strict` (or Lax for OAuth return).
- Short access TTL + rotating refresh; Redis blacklist on logout; never put PHI in JWT claims.

## HTTP security headers

Middleware sets: `X-Content-Type-Options`, `X-Frame-Options: DENY`, CSP, Referrer-Policy, Permissions-Policy, `Cache-Control: no-store`, and HSTS outside local/test.

## Sessions

| Control | Status |
|---------|--------|
| Access + refresh JTIs registered in Redis | Done |
| Refresh rotation (revoke old refresh) | Done |
| Refresh requires live Redis session key | Done (tightened) |
| Logout blacklists access + refresh | Done |

## MFA recovery runbook (ops)

1. User presents a remaining backup code at login (`backup_code`) or authenticated disable.
2. Codes are single-use; exhausted codes require admin break-glass (out of band identity proof) — not automated in this scaffold.
3. Re-enroll TOTP after disable; store new backup codes offline.

## Explicit non-goals

- Formal HIPAA certification / BAA negotiation
- Live HIBP network calls in unit tests
- `terraform apply` (scaffold + comments only)
