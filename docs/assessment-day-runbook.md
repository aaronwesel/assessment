# Assessment-day runbook

Operational playbook for the timed take-home / live assessment. Prep scaffolding lives on this repo; domain work starts only after the prompt is locked.

## Before the clock

1. Confirm AWS identity and region (no `terraform apply` until Stream G is ready).
2. Copy `.env.example` → `.env`; fill OAuth / JWT / keys **locally** only.
3. `docker compose up --build` — verify db + redis healthy; replace API/frontend stubs when siblings land.
4. Skim [HIPAA checklist](./hipaa-checklist.md); treat rows as gates, not certification.

## Prompt lock (Stream A) — first 15–30 min

- Cut **MVP vs stretch**; write a one-page ADR (scope, non-goals, tenancy assumption).
- Do **not** start domain models until MVP is locked.
- Broadcast locked scope to all streams.

## Parallel streams

| Stream | Owns | Depends on |
|--------|------|------------|
| A Spec & slice | MVP lock, ADR | Prompt |
| B Domain API | Models, migrations, workflow APIs | A; auth stubs |
| C Workers | Queues, idempotency, SSRF, limits | B contracts |
| D UI | Authenticated flows; keep UI simple | A + auth shell; B |
| E Auth finish | One Tap wiring, RBAC, tenancy | OAuth apps; stubs |
| F Test & harden | Auth/tenancy/SSRF/E2E suites | B–E |
| G Infra & HIPAA | Terraform fill, secrets, deploy smoke | AWS account |
| H AI (optional) | DeepSeek / Rekognition behind flags | B + keys; MVP green |

**Suggested waves**

1. A locks MVP.
2. E ∥ B ∥ G
3. C ∥ D (after B contracts + protected routes)
4. F continuous; H only if MVP green
5. F gate → G deploy smoke → demo + README update

## Health gates

| Gate | Command / check |
|------|-----------------|
| Compose | `docker compose ps` — db/redis healthy; api/frontend respond |
| API health | `GET /health` → 200 (once real API ships) |
| CI | lint · test · docker · terraform fmt/validate green on the PR |
| Terraform | `terraform fmt` + `validate` only until explicit apply decision |
| HIPAA | Checklist rows for MVP path marked done or explicitly deferred with owner |

## Secrets & PHI

- Never paste secrets into chat, commits, or screenshots.
- No PHI in logs, fixtures, or DeepSeek prompts.
- Prefer Secrets Manager in AWS; local `.env` is Compose-only.

## Backup / restore drill (note)

When Aurora/RDS is stood up:

1. Confirm automated backups + PITR window.
2. Document restore target (new instance vs overwrite) and who runs it.
3. Smoke: restore to isolated instance, connectivity check, tear down.

## Demo order (close)

1. Auth: password + at least one One Tap path (or stubbed exchange).
2. Primary MVP workflow (start → progress → result).
3. Tenant isolation / deny path (one failing unauthorized call).
4. Infra: architecture sketch + encryption/secrets story (no live apply required for demo if staging is up).
5. AI only if flag-on and non-PHI.

## Break-glass

- No open admin endpoints in prod-like envs.
- Emergency access: documented IAM role / break-glass user owned by the human operator; rotate after use.
- If blocked on user OAuth consoles or AWS apply permissions, record on the “blocked on user” list and continue other streams.

## Explicit do-nots

- Do not `terraform apply` from CI.
- Do not commit `.env`, keys, or dumps.
- Do not expand into stretch/AI until F says MVP is green.
