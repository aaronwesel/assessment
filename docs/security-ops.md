# Security / platform ops smoke notes

Workstream A surfaces for audit, break-glass, consent, signed media, feature flags,
health/SLOs, and webhook outbox. Pair with `docs/security.md` and `docs/hipaa-checklist.md`.

**HIPAA:** never put emails, MRNs, diagnoses, or free-text patient content in audit
metadata, break-glass details, webhook payloads, Slack/SES bodies, or media object keys.

## Prerequisites

- API running locally (`docker compose up` or `uvicorn app.main:app`).
- Admin JWT (register a user, set `users.role=admin`, mint access token) or use an
  existing admin from seed tooling.
- Redis optional locally (in-memory stub works for flags).

## Audit log + export

```bash
# Structured admin note (codes only)
curl -sS -X POST "$API/api/v1/audit/events" \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"action":"ops_note","resource_type":"system","metadata":{"env":"local"}}'

# List + export
curl -sS "$API/api/v1/audit/events" -H "Authorization: Bearer $ADMIN_TOKEN"
curl -sS "$API/api/v1/audit/export?format=csv" -H "Authorization: Bearer $ADMIN_TOKEN" -o audit.csv
curl -sS "$API/api/v1/audit/export?format=json" -H "Authorization: Bearer $ADMIN_TOKEN" -o audit.json
```

Expect opaque `actor_user_id` values and snake_case actions — no PHI fields.

## Break-glass / emergency access

```bash
curl -sS -X POST "$API/api/v1/break-glass/grants" \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"target_user_id":"USER_UUID","reason_code":"mfa_recovery","ttl_minutes":30,"ticket_ref":"INC-1"}'

curl -sS "$API/api/v1/break-glass/grants?active_only=true" -H "Authorization: Bearer $ADMIN_TOKEN"
curl -sS -X POST "$API/api/v1/break-glass/grants/GRANT_ID/revoke" -H "Authorization: Bearer $ADMIN_TOKEN"
```

- `reason_code` is mandatory (snake_case taxonomy).
- SES/Slack hooks fire when those features are enabled; bodies stay PHI-free.
- Grants are time-boxed (`BREAK_GLASS_*_TTL_MINUTES`).

## Consent + AI gate

```bash
curl -sS "$API/api/v1/consent/purposes" -H "Authorization: Bearer $USER_TOKEN"
curl -sS -X PUT "$API/api/v1/consent/me" \
  -H "Authorization: Bearer $USER_TOKEN" -H "Content-Type: application/json" \
  -d '{"purpose":"ai_deepseek","granted":true}'
```

DeepSeek / Rekognition helpers (`complete_for_subject`, `detect_labels_for_subject`)
refuse calls without consent and when Redis kill switches (`ai_deepseek` /
`ai_rekognition`) are off.

## Signed media URLs

```bash
curl -sS -X POST "$API/api/v1/media/signed-url" \
  -H "Authorization: Bearer $USER_TOKEN" -H "Content-Type: application/json" \
  -d '{"object_key":"org/demo/uuid.bin","method":"GET","ttl_seconds":300}'
```

Stub HMAC URLs mimic S3 presign TTL behavior. Prefer opaque keys; do not log full URLs.

## Feature flags / kill switches

```bash
curl -sS "$API/api/v1/flags" -H "Authorization: Bearer $ADMIN_TOKEN"
curl -sS -X PUT "$API/api/v1/flags/slack" \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"enabled":false}'
curl -sS -X DELETE "$API/api/v1/flags/slack" -H "Authorization: Bearer $ADMIN_TOKEN"
```

Redis key `flag:{name}`. Unset → Settings default. Explicit `0` kills the feature.

## Health + SLOs

| Path | Purpose |
|------|---------|
| `GET /health` | Liveness (ALB) |
| `GET /ready` or `/health/ready` | Readiness + flag snapshot |
| `GET /health/metrics` | Tiny counters |
| `GET /health/slo` | Availability / p95 targets |

## Webhook outbox

```bash
curl -sS -X POST "$API/api/v1/webhooks/outbox" \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"event_type":"team_updated","destination_url":"https://example.com/hook","payload":{"team_id":"t1"}}'

curl -sS -X POST "$API/api/v1/webhooks/outbox/process" -H "Authorization: Bearer $ADMIN_TOKEN"
```

Deliveries include `X-Assessment-Signature: sha256=...` over the raw body
(`WEBHOOK_SIGNING_SECRET`). Failed attempts retry with exponential backoff until
`dead`.

## Stub mounts for other workstreams

- `GET /api/v1/billing/status` → workstream B (Stripe)
- `GET /api/v1/growth/status` → workstream C (landing / superadmin)
