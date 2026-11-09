# HIPAA engineering checklist

**Scope:** Practical engineering controls required before calling this SaaS **complete**.  
**Not a claim:** This is HIPAA-*ready* posture — not a BAA, audit attestation, or certification.

Use as acceptance criteria during prep and assessment day. Infra stream owns Terraform defaults; app streams own auth, audit, and log hygiene.

## Controls

| Control | Engineering bar | Status (prep) |
|---------|-----------------|---------------|
| Encryption in transit | TLS on ALB; Redis TLS/AUTH in AWS; DB SSL required in non-local envs | Scaffold / TODO |
| Encryption at rest | RDS/Aurora + Redis + log/object buckets via KMS CMKs | Scaffold / TODO |
| Secrets | Secrets Manager / SSM only; never commit secrets; document rotation | `.env.example` only; no secrets in git |
| Network | Private subnets for ECS, DB, Redis; public only ALB; SG least privilege | Terraform sibling |
| Identity | Authenticated API; RBAC; tenant isolation on every query | Backend sibling |
| Audit logging | Structured events (who / what / when / tenant); CloudWatch or immutable bucket | TODO |
| No PHI in logs | Redact bodies; forbid PHI in prompts/logs; DeepSeek sanitized / feature-gated | Flag off by default |
| No PHI in email | SES alert templates forbid PHI/PII in bodies; login alerts omit account identifiers | `SES_ENABLED` / `LOGIN_ALERT_EMAIL_ENABLED` off by default |
| No PHI in Slack | Incoming webhook payloads use opaque ids/event codes only; invite emails never posted | `SLACK_ENABLED=false` by default |
| Backups | Automated backups + PITR on Aurora/RDS; restore drill in runbook | Runbook noted |
| Least privilege IAM | Task roles scoped per service; no `*` on data stores | Terraform sibling |
| WAF / abuse | WAF placeholder + Redis rate limits; SSRF deny-lists on workers | LB Redis rate-limit middleware + login lockout stubs |
| MFA | TOTP enroll/verify; backup codes; require flag on login | See `docs/security.md` |
| Password policy | bcrypt + complexity + common-password denylist | See `docs/security.md` |
| BAA-minded services | Prefer AWS services eligible for BAA; document AI vendor BAA gap — DeepSeek off or non-PHI only until cleared | Documented |
| Migrations | Run as deploy step, not on API boot in prod | Convention |
| Access | No open admin; break-glass documented | Runbook |

## AI / third-party notes

- DeepSeek and similar LLM vendors may **not** be under a customer BAA. Keep `DEEPSEEK_ENABLED=false` until legal clears, or send **non-PHI only**.
- Amazon Rekognition: use IAM task roles (no long-lived keys in app where avoidable); treat image content as sensitive.
- Prefer AWS services that can sit under an AWS BAA for storage, compute, and logs.

## Definition of done (compliance slice)

- [ ] TLS everywhere outside local Compose
- [ ] KMS encryption at rest for DB, Redis, and audit/log storage
- [ ] No secrets in repo or images
- [ ] Auth + RBAC + tenant isolation enforced and tested
- [ ] Audit trail for privileged and PHI-adjacent actions
- [ ] PHI redaction verified in log samples
- [ ] Backup/PITR enabled; restore steps in runbook
- [ ] SSRF / rate-limit tests green
- [ ] AI features off or non-PHI with documented BAA gap

## Explicit non-goals (until separately scoped)

- Formal HIPAA certification or third-party audit
- Signed BAAs negotiated during the timed assessment
- Full chaos / pen-test engagement
