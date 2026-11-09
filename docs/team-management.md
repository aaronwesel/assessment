# Team management + optional Slack

Goal: parcel assessment work to different developers with clear ownership, and optionally notify a Slack channel for invites, deploy/alerts, and PR handoffs — **without ever putting PHI in Slack**.

## Domain model (backend stubs)

| Entity | Purpose |
|--------|---------|
| **Organization** | Tenant container (`name`, `slug`, `created_by_user_id`) |
| **Team** | Named group under an org + `workstream` (`api\|ui\|infra\|mobile\|ai\|general`) |
| **TeamMembership** | User and/or pending `invite_email` + **role** |

### Membership roles

| Role | Meaning |
|------|---------|
| `owner` | Full team lifecycle (seeded on create) |
| `admin` | Invite members, help steer the stream |
| `developer` | Implement and own PRs |
| `viewer` | Read / review without merge ownership |

App-wide JWT RBAC (`user` / `admin` / `moderator`) stays separate — a platform admin can still be a `viewer` on a feature team.

## HTTP APIs (JWT Bearer required)

| Method | Path | Behavior |
|--------|------|----------|
| `POST` | `/api/v1/teams` | Create team (inline org create via `organization` or existing `organization_id`); creator becomes `owner` |
| `GET` | `/api/v1/teams` | List teams for the current subject (optional `organization_id`) |
| `POST` | `/api/v1/teams/{team_id}/invites` | Invite by email (**stub** — persists pending membership; no mail send yet) |

Invite responses intentionally omit the email address to reduce accidental PII capture in gateway logs. Clients already know the address they submitted.

## Repo workflow (human parceling)

- **CODEOWNERS** — path → team label mapping for `backend/`, `frontend/`, `infra/`, `apps/` (and AI client files).
- **[workstreams.md](./workstreams.md)** — how to assign API / UI / infra / mobile / AI streams to developers.
- **[CONTRIBUTING.md](../CONTRIBUTING.md)** — PR ownership and handoff expectations.

## Slack integration (optional)

### Feature flags

```bash
SLACK_ENABLED=false          # default
SLACK_WEBHOOK_URL=           # Incoming Webhook URL — Secrets Manager in AWS
```

When disabled (default), the Slack client is a no-op. When enabled, the app posts short, PHI-free text via an **Incoming Webhook**.

### Notification kinds (prep)

| Kind | When | Payload shape (no PHI) |
|------|------|-------------------------|
| Team invite / create | Team create or invite stub | `event`, `team_id`, `workstream`, `role` |
| Deploy / alert | Ops hook (callable from future deploy jobs) | `event`, `env`, `severity` |
| PR handoff | Explicit baton pass | `event`, `workstream`, optional `pr=#` |

**Never** include: patient data, invite emails, display names, tokens, or free-text user content.

### Incoming webhook vs Slack app (later)

| Approach | Pros | Cons | When to use |
|----------|------|------|-------------|
| **Incoming Webhook** (this scaffold) | One secret URL; no OAuth; enough for channel posts | No interactive buttons, no Events API, one channel per URL (unless multiple webhooks) | Prep + early assessment handoffs/alerts |
| **Slack app** (Bot Token + Events / Interactivity) | Slash commands, modals, per-user DMs, richer handoff UX | App install, scopes, rotation, more IAM/secrets surface | After webhook proves useful; when interactive assign/ack is needed |

Migration path: keep the `SlackNotifier` protocol; swap `IncomingWebhookSlackClient` for a Bot API client without changing team service call sites. Document the new bot token in Secrets Manager the same way as the webhook.

### Secrets Manager (Terraform note only)

`infra/terraform/modules/secrets` includes placeholder keys `SLACK_WEBHOOK_URL` and `SLACK_ENABLED` on the app secret JSON shell. **Do not `terraform apply`** during prep — see `infra/terraform/DO_NOT_APPLY.md`.

## HIPAA reminders

- Slack and email are **egress** channels: treat them like logs — allowlisted tokens only.
- Reuse `assert_no_phi` before any webhook post.
- Leave `SLACK_ENABLED=false` unless the webhook URL is real and message templates stay opaque.

## Tests

```bash
cd backend && pytest -q tests/test_teams.py tests/test_slack.py
```
