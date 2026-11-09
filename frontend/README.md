# Frontend

React + Vite + TypeScript auth shell for the assessment prep scaffold.

## What’s included

- Login: Google One Tap placeholder, Microsoft button placeholder, password form stub
- Authenticated empty home (no domain screens)
- Minimal top nav
- Client ID / API base URL via Vite env (`VITE_*`)

## Setup

```bash
cp .env.example .env
npm install
npm run dev
```

| Variable | Purpose |
|----------|---------|
| `VITE_GOOGLE_CLIENT_ID` | Google Identity Services client ID (One Tap) |
| `VITE_MICROSOFT_CLIENT_ID` | Microsoft Entra / MSAL app client ID |
| `VITE_API_BASE_URL` | Backend API base (token exchange) |

Social buttons stay disabled until the matching client ID is set. Password sign-in is a local stub until the API auth endpoints exist.

## Scripts

- `npm run dev` — local Vite server
- `npm run build` — typecheck + production build
- `npm run preview` — preview production build
- `npm run lint` — oxlint
