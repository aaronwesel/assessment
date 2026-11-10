# Workstreams — assigning developers

How to parcel assessment work across developers using **teams**, **CODEOWNERS**, and optional **Slack** handoffs.

## Streams

| Stream | Path focus | Typical owner role | GitHub team label (prep) |
|--------|------------|--------------------|---------------------------|
| **API** | `backend/` (routes, models, auth, Alembic) | `developer` / `admin` | `@assessment/api-team` |
| **UI** | `frontend/` | `developer` | `@assessment/ui-team` |
| **Infra** | `infra/`, Compose, CI workflows | `developer` / `admin` | `@assessment/infra-team` |
| **Mobile** | `apps/android/`, `apps/ios/` | `developer` | `@assessment/mobile-team` |
| **AI** | DeepSeek / Rekognition clients + flags | `developer` | `@assessment/ai-team` |

Cross-cutting (auth contracts, HIPAA checklist, secrets): `@assessment/platform-leads` with `owner` or `admin` on the org-wide team.

## Assigning a stream to a developer

1. **Create / pick a Team** via `POST /api/v1/teams` (JWT) with `workstream` set to `api|ui|infra|mobile|ai`.
2. **Invite** with `POST /api/v1/teams/{id}/invites` (`role=developer|admin|viewer`). Email send is a stub in prep; membership row is the source of truth.
3. **Map PRs to paths** so CODEOWNERS requests the right team. Prefer one stream per PR when the clock is tight.
4. **Optional Slack**: with `SLACK_ENABLED=true` and a webhook, create/invite emits a PHI-free “team invite” notice; use PR handoff / deploy alerts for baton passes (see [team-management.md](./team-management.md)).

## Roles (team membership)

| Role | Intent |
|------|--------|
| `owner` | Team lifecycle; seeded on create |
| `admin` | Invite + reassign streams |
| `developer` | Implement and own PRs in the stream |
| `viewer` | Read / review without merge ownership |

These are **orthogonal** to app RBAC (`user` / `admin` / `moderator` in JWT).

## Day-of checklist

- [ ] Each stream has at least one `developer` membership
- [ ] CODEOWNERS team handles exist (or lead reviews manually)
- [ ] Slack left **off** unless the webhook is in Secrets Manager and copy stays PHI-free
- [ ] No PHI in PR titles, Slack text, or invite-related logs
