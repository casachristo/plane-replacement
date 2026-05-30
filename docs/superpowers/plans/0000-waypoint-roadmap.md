# Waypoint Implementation Roadmap

> **Spec:** `docs/superpowers/specs/2026-05-30-waypoint-design.md`

Waypoint ships in 7 phases. Each phase produces working, testable software on its own and gets a dedicated plan document in `docs/superpowers/plans/`. After a phase ships and its plan is closed out, the next phase's plan is written.

## Phase order

| # | Phase | Plan doc | Ships | Depends on |
|---|---|---|---|---|
| 1 | Core API + data layer | `YYYY-MM-DD-waypoint-phase1-core-api.md` | Runnable .NET API + Postgres; CRUD on projects/issues/comments with workflow-validated transitions; integration tests via Testcontainers; `docker compose up` works locally; **no auth** (placeholder `[AllowAnonymous]`) | — |
| 2 | Internal surface + Cairn-ready contracts | `phase2-internal-surface.md` | Two Kestrel ports; `IPrincipalResolver` abstraction; `ServiceBearerResolver` on `:8081`; `Waypoint.Contracts` published as NuGet; seed-token CLI; intent endpoints | 1 |
| 3 | Importer + repeated dry runs | `phase3-importer.md` | Standalone `Waypoint.Importer` console; dump phase; load phase with all mapping rules; golden-file tests; repeated dry-runs against prod Plane yielding clean reports | 1 (DB schema) |
| 4 | Public auth + minimal SPA | `phase4-public-spa.md` | `OidcSessionResolver` for `:8080`; Authelia client registration; Next.js SPA scaffold with project list, issue list, kanban view, issue detail page; login/logout flows | 1, 2 |
| 5 | Webhooks + admin + audit | `phase5-webhooks-admin.md` | Webhook subscription CRUD; delivery dispatcher with HMAC signing + retry; `/admin/tokens` SPA page; `/admin/audit` log viewer | 4 |
| 6 | Deploy + cutover | `phase6-deploy-cutover.md` | Helm chart; NetworkPolicies; backup CronJob; Cairn-side `IIssueTracker` swap (separate Cairn-repo ticket); the actual maintenance-window cutover; `helm uninstall plane` | 1–5 |
| 7 | Mutation testing CI + observability | `phase7-mutation-observability.md` | Stryker configs for Domain/Api/Importer/SPA-lib; CI gates including zero-tolerance for surviving auth mutations; Prometheus metrics endpoint; Grafana dashboards | 1–5 |

## Per-phase scope and explicit deferrals

### Phase 1 — Core API + data layer

**In scope:**
- Solution scaffold: `Waypoint.Api`, `Waypoint.Domain`, `Waypoint.Contracts`, `Waypoint.Architecture.Tests`, `Waypoint.Api.Tests`, `Waypoint.Domain.Tests`
- Entities: Project, State, IssueType, Workflow, WorkflowTransition, Issue, Comment, Activity, User
- Endpoints: project CRUD, issue CRUD + transition + activity, comment CRUD
- Cross-cutting middleware: error envelope, idempotency, request-id
- Cursor pagination
- Integration tests via Testcontainers.PostgreSql
- Single-port `:8080` only, no auth
- `docker compose` for local dev

**Explicitly deferred:**
- Entities: Epic, Cycle, Label, Component, IssueIntent, WebhookSubscription, WebhookDelivery, ApiToken, TokenAuditLog, UserSession, Attachment → these come in Phase 2 (auth-related), Phase 5 (webhooks/admin), or as needed
- Any auth pipeline → Phase 2 + Phase 4
- The second Kestrel port → Phase 2
- The SPA → Phase 4
- Mutation testing → Phase 7
- Deployment to K3s → Phase 6

### Phase 2 — Internal surface + Cairn-ready contracts

**In scope:**
- Second Kestrel endpoint at `:8081` with separate middleware pipeline
- `IPrincipalResolver` abstraction
- `ServiceBearerResolver` implementation
- `Waypoint.Importer` no — that's Phase 3
- `api_tokens` + `token_audit_log` entities + migrations
- `seed-token` CLI mode in `Waypoint.Api`
- `IssueIntent` entity + intent endpoints on `:8081`
- `Waypoint.Contracts` NuGet packaging (project becomes publishable, version stamping, snapshot tests)
- Architecture tests enforcing the public/internal namespace split

**Explicitly deferred:**
- Public OIDC auth → Phase 4
- Cairn's swap from `PlaneClient` to `WaypointClient` → Cairn-repo ticket, scheduled in Phase 6 cutover

### Phase 3 — Importer + dry runs

**In scope:**
- `Waypoint.Importer` console app
- Dump phase: pulls all Plane resources to local JSON tree
- Load phase: parses dump, maps to Waypoint entities, writes to DB
- All mapping rules from spec (HTML→MD, label-as-epic detection, attachment rewrite, etc.)
- Golden-file fixtures in `tests/Waypoint.Importer.Tests/fixtures/`
- Dry-run report (row counts, sample diffs, anomaly list)
- Repeated production-Plane dry-runs to surface and fix edge cases

**Explicitly deferred:**
- Epic/Cycle/Label/Component entities → must be added to the data model before this phase can complete, so Phase 3 includes those migrations as a prerequisite
- The actual cutover → Phase 6
- Attachment storage backend (the storage_key destination) → decided here based on what the cluster already runs (MinIO, hostPath, etc.)

### Phase 4 — Public auth + minimal SPA

**In scope:**
- `OidcSessionResolver` for `:8080`
- Authelia OIDC client registration (homelab-side config)
- `users` extensions for OIDC fields, `user_sessions` table
- Auth endpoints: `/auth/login`, `/auth/callback`, `/auth/logout`, `/api/v1/whoami`
- Next.js 15 SPA scaffold with App Router, TanStack Query, Tailwind, shadcn/ui
- SPA pages: project list, issue list (table view), kanban board, issue detail, comment thread
- Wiremock-based OIDC test harness

**Explicitly deferred:**
- Admin pages (tokens, audit) → Phase 5
- Webhook subscription UI → Phase 5
- Rich-text editing affordances (markdown preview tab is sufficient for Phase 4)

### Phase 5 — Webhooks + admin + audit

**In scope:**
- `webhook_subscriptions` + `webhook_deliveries` entities + migrations
- Subscription management endpoints
- Delivery dispatcher as `IHostedService` in `Waypoint.Api`
- HMAC signing of payloads, retry with backoff, dead-letter
- SPA pages: `/admin/tokens` (create/revoke service tokens), `/admin/audit` (token_audit_log viewer)
- Admin-scope enforcement based on Authelia groups

**Explicitly deferred:**
- Per-IP rate limiting on the public surface (defense-in-depth at Traefik) → Phase 6 deployment task
- Webhook receiver fan-out optimizations → only if measured slow

### Phase 6 — Deploy + cutover

**In scope:**
- Multi-stage Dockerfiles for `Waypoint.Api` and `waypoint-web`
- Gitea Actions (or build script) to push images to `gitea.chris.box/chris/waypoint-{api,web}`
- Helm chart in `deploy/helm/waypoint/`: Deployments, Services, IngressRoute, NetworkPolicies, Secrets, CronJob, pre-upgrade migration Job
- TLS via the homelab CA (mechanism matching existing media-ns services)
- Cairn-side ticket in Cairn's repo: DI swap, Secret mount, `app: cairn-api` label confirmation
- Cutover plan execution per spec § Migration
- `helm uninstall plane`; retain Plane PVC 30d

**Explicitly deferred:**
- Mutation testing CI → Phase 7
- Grafana dashboards → Phase 7

### Phase 7 — Mutation testing CI + observability

**In scope:**
- `Stryker.NET` configs per .NET project (Domain 85%, Api/Auth zero-tolerance, Importer/Mapping 80%)
- StrykerJS config for SPA `web/lib/`
- CI gates: per-PR mutation runs on changed projects, nightly full-suite trending
- Prometheus `/metrics` endpoint with custom metrics (webhook delivery, token use, importer progress)
- NetworkPolicy clause for monitoring namespace
- Grafana dashboards for API latency, webhook delivery success, token activity

**Explicitly deferred (out of project scope):**
- Anything in the Cairn codebase — Cairn's own quality gates are Cairn's concern

## Cross-phase invariants

These hold across every phase; called out so each plan doesn't have to repeat them:

1. **TDD discipline**: write the failing test first, watch it fail, write the minimum to pass, watch it pass, commit. No exceptions.
2. **Conventional commits**: `feat(scope): ...`, `fix(scope): ...`, `chore(scope): ...`, etc. Scope = component name (e.g. `api`, `domain`, `importer`).
3. **No work merges to `main` without integration tests green**. Phase plans include the test runs as explicit steps.
4. **Migrations are forward-only**. EF migrations get committed alongside the entity changes that produced them. No squashing.
5. **Architecture tests run on every push from Phase 2 onward** (Phase 1 has the project but only one rule).
6. **No secrets in the repo**. Secrets go in K8s Secrets (Phase 6) or `appsettings.Development.json` placeholders that the build pipeline replaces.

## Out of scope for all phases

- Mobile app
- Plane → Waypoint live-write bridge (the spec explicitly says one-shot cutover, no dual-write)
- Multi-workspace support — single workspace assumption per the spec
- Custom field system (explicit Jira-rejection in the spec)
- ADF or HTML-as-source-of-truth (explicit spec decision)
