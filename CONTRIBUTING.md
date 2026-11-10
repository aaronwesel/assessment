# Contributing

Prep scaffold for a timed, HIPAA-minded assessment. Prefer small, stream-scoped PRs with a clear owner.

## PR ownership

1. **One primary stream per PR** when possible (`api`, `ui`, `infra`, `mobile`, `ai`) — see [docs/workstreams.md](docs/workstreams.md).
2. **CODEOWNERS** (`.github/CODEOWNERS`) requests review from the path’s team label. Replace `@assessment/...` placeholders with real GitHub teams when available.
3. **Author is the owner** until merge or an explicit handoff (PR comment + optional Slack `pr_handoff` event). Do not silent-drop a stream.
4. **Draft PRs** are fine for early feedback; mark ready only when tests you own are green locally.
5. **Do not** open a second PR that rewrites another stream’s files without coordinating — use stacked or sequential PRs instead.

## Branching

- Feature work: branch from the agreed base (`main` or the active prep integration branch).
- Keep commits focused; push to your branch; open/update one PR per concern.

## Checks before you ask for review

- [ ] `pytest` (backend) or frontend lint/build for UI changes
- [ ] No secrets, PHI, or real webhook URLs in the diff
- [ ] Terraform: `fmt` / `validate` only — **never `apply`** from prep
- [ ] Comments on non-obvious new code (assessment preference)

## Security / HIPAA hygiene

- Never commit `.env` secrets or Slack webhook URLs.
- Never put PHI in logs, SES bodies, or Slack messages.
- Prefer feature flags defaulting to **off** for optional integrations (SES, Slack, DeepSeek, Rekognition).

## Team invites

Use the JWT team APIs (`/api/v1/teams`) to record ownership. Invite email delivery is stubbed in prep; membership rows still matter for parceling work. Details: [docs/team-management.md](docs/team-management.md).
