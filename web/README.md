# Waypoint Web

Next.js 15 SPA for Waypoint. Single-tenant, server-rendered, talks to the Waypoint API
via path-rewritten proxy (`/api/v1/*` → backend).

## Phase 4 scope

- `/` — landing page
- `/projects` — project list
- `/projects/[slug]` — project detail with issue list

Authentication is handled by the Waypoint API (OIDC against Authelia). The SPA simply
links to `/auth/login` which initiates the flow; the resulting `waypoint_session` cookie
is sent automatically with API requests via the Next.js rewrite.

## Deferred to Phase 4 follow-up

- Kanban board view
- Issue detail page with comment thread
- Filter / search UI

## Local dev

```
npm install
WAYPOINT_API_BASE=http://localhost:8080 npm run dev
```

Then open `http://localhost:3000`. The Waypoint API must be running locally
(`dotnet run --project ../src/Waypoint.Api`) for the data calls to succeed.
