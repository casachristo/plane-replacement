# Waypoint — Design

**Status:** Draft for review
**Author:** Chris (with Claude)
**Date:** 2026-05-30
**Repo:** `plane-replacement` (Gitea, to be created)

## Why

Plane Community is no longer fit for purpose:

- **Throughput**: 429s under burst load. A 20-call briefing burst costs ~111s end-to-end. Cairn's responsiveness is bounded by Plane's throughput because issue sync sits on the briefing request path.
- **Modeling**: no first-class issue types or epics — both are hacked with `type:` and `epic:` label conventions. Every consumer has to know the convention.
- **Operational surface**: two systems (Plane + Cairn) means two auth models, two webhook lag windows, two pieces to onboard new tooling against.
- **Agent ergonomics**: descriptions stored as HTML force agents to hand-roll markup.

Waypoint replaces Plane wholesale. There is no live Plane fallback after cutover; a `pg_dump` is kept 30 days as cold backup only.

## Vocabulary (locked)

- **Epic** — ticket-side, cross-module feature. First-class table.
- **Issue type** — Bug / Task / Story / Spike. First-class column on issues. Borrowed from Jira.
- **Component** — per-project sub-area with an owner. First-class. Borrowed from Jira.
- **Module** — code-side area, declared by a `.cairn` descriptor in a repo. Lives in Cairn, not Waypoint.
- **Cycle** — time-boxed milestone (sprint). First-class but UI-optional.

## Scope decisions (locked)

1. **Standalone repo.** Waypoint is not a feature of the Cairn codebase. Cairn calls Waypoint over HTTP via its existing `IIssueTracker` interface (rename in Plane ticket #2).
2. **Backend + own SPA in this repo.** Full Plane replacement, not a backend-only service. Cairn does not host Waypoint's UI.
3. **Stack:** .NET 9 minimal API + Postgres + Next.js + (no MCP — see #6).
4. **Markdown is the only description format.** No HTML caching, no ADF. One source of truth, regenerated for display on demand.
5. **Jira-borrowed modeling: issue types, components, workflows.** Jira-rejected: custom fields, ADF, schemes.
6. **No MCP server in Waypoint.** Agents are forced through Cairn's existing MCP. Cairn enforces per-agent scopes, intent-locks, and rate limits before any call reaches Waypoint.
7. **Two physical surfaces.** Public (`:8080`) for humans via OIDC; Internal (`:8081`) for Cairn via bearer + NetworkPolicy. Three independent enforcement layers on the internal path (no external DNS, NetworkPolicy restricts to Cairn pods, bearer token check).
8. **Modular auth store.** All auth flows through `IPrincipalResolver`; swap Authelia for any other OIDC provider (or non-OIDC mechanism) by changing config or registering a different resolver. No code outside the resolver knows about Authelia.
9. **Mutation testing as a first-class quality gate.** Auth pipeline is held to zero-surviving-mutations; domain layer to 85%.
10. **One-shot cutover.** No dual-write window, no live Plane after cutover. Importer is replayable and dry-runnable; safety comes from rehearsing imports, not from keeping Plane alive.

## Architecture

### Repo layout

```
plane-replacement/
├── src/
│   ├── Waypoint.Api/                # ASP.NET Core minimal API + in-process WebhookDispatcher
│   ├── Waypoint.Contracts/          # Wire types published as NuGet — consumed by Cairn
│   ├── Waypoint.Domain/             # EF Core entities, DbContext, migrations, domain logic
│   ├── Waypoint.Importer/           # Console tool: Plane API → Waypoint DB
│   └── Waypoint.Architecture.Tests/ # NetArchTest module-boundary guards
├── tests/
│   ├── Waypoint.Api.Tests/          # WebApplicationFactory + Testcontainers Postgres
│   ├── Waypoint.Importer.Tests/     # Golden-file Plane fixtures → expected Waypoint rows
│   └── Waypoint.Contracts.Tests/    # Snapshot tests on wire-type JSON shape
├── web/                             # Next.js 15 SPA — distinct from src/, no shared parent
├── deploy/
│   ├── helm/waypoint/               # Helm chart, mirrors existing media-ns chart conventions
│   └── traefik/                     # IngressRoute (public only)
├── docs/superpowers/specs/          # design docs
└── .cairn                           # Cairn project descriptor
```

`src/` (.NET) and `web/` (Next.js) are top-level peers. No `apps/` wrapper.

### Process topology

```
K3s namespace: media

Deployments:
  waypoint-api    — single pod, two container ports (8080 public, 8081 internal)
  waypoint-web    — Next.js standalone

StatefulSets:
  waypoint-postgres — single pod, PVC 50Gi, postgres-exporter sidecar

Services:
  waypoint-public    ClusterIP → waypoint-api:8080
  waypoint-internal  ClusterIP → waypoint-api:8081  [no IngressRoute, no external DNS]
  waypoint-web       ClusterIP → waypoint-web:3000
  waypoint-postgres  ClusterIP → waypoint-postgres:5432

IngressRoute (public only):
  waypoint.chris.box + /api/v1/* or /auth/* → waypoint-public
  waypoint.chris.box                        → waypoint-web
  TLS: cert from the existing *.chris.box homelab CA (issuance mechanism matches your other media-ns services)

NetworkPolicies:
  waypoint-api-restrict
    :8080 from {} (any in-cluster — Traefik fronts the internet)
    :8081 from {podSelector: app=cairn-api}
  waypoint-postgres-restrict
    :5432 from {podSelector: app IN (waypoint-api, waypoint-backup)}

CronJob:
  waypoint-postgres-backup  daily 03:00 UTC; pg_dump → MinIO; 14d/4w/6m retention
```

The internal port has *three independent* protections: no Traefik route exists (unreachable from outside the cluster), NetworkPolicy denies all non-Cairn pods, and the bearer token check rejects unauthenticated requests. Bypassing requires defeating all three.

## Data model

Postgres, EF Core code-first migrations. Common columns on all tables: `id uuid PK default gen_random_uuid()`, `created_at timestamptz`, `updated_at timestamptz`, `deleted_at timestamptz NULL` (soft delete). Per-project counters via Postgres sequences.

| Table | Purpose | Notable columns |
|---|---|---|
| `projects` | Top-level container | `slug` (unique), `name`, `identifier` (3-letter, e.g. `WAY`), `default_state_id`, `archived_at` |
| `issues` | The ticket | `project_id`, `sequence_id` (per-project counter), `title`, `description_md`, `state_id`, `priority` (urgent/high/medium/low/none), `issue_type_id`, `epic_id NULL`, `cycle_id NULL`, `parent_issue_id NULL`, `assignee_ids uuid[]`, `due_date NULL`, `external_id NULL`, `external_source NULL` |
| `issue_types` | Bug / Task / Story / Spike / custom | `project_id`, `name`, `description`, `default_workflow_id`, `is_default` |
| `epics` | Cross-module feature container | `project_id`, `sequence_id` (separate counter, prefix `WAY-E-N`), `title`, `description_md`, `status` (planned/in-flight/done), `target_cycle_id NULL` |
| `cycles` | Time-boxed milestone | `project_id`, `name`, `start_date`, `end_date`, `state` (upcoming/active/completed) |
| `states` | Per-project workflow states | `project_id`, `name`, `group` (backlog/unstarted/started/completed/cancelled), `color`, `sort_order`, `is_default` |
| `workflows` | Per-project per-type state machine | `project_id`, `name`, `description` |
| `workflow_transitions` | FK-constrained edges between states | `workflow_id`, `from_state_id`, `to_state_id` |
| `labels` | | `project_id`, `name`, `color`, `parent_label_id NULL` |
| `issue_labels` | M2M | `issue_id`, `label_id` |
| `components` | Per-project sub-area with owner | `project_id`, `name`, `description`, `owner_user_id NULL` |
| `issue_components` | M2M | `issue_id`, `component_id` |
| `comments` | | `issue_id`, `body_md`, `author_id` (or `actor_token_id`), `parent_comment_id NULL` |
| `attachments` | | `issue_id` or `comment_id`, `filename`, `size`, `mime`, `storage_key`, `uploaded_by` |
| `issue_intents` | Lock-acquire intent declarations from Cairn | `project_id`, `module_path`, `intent_text`, `declared_by_token_id`, `lock_acquired_at`, `released_at NULL`, `linked_issue_id NULL` |
| `webhook_subscriptions` | Outbound webhooks | `project_id NULL`, `target_url`, `event_mask`, `secret`, `is_active`, `last_delivery_at` |
| `webhook_deliveries` | Append-only delivery log | `subscription_id`, `event`, `payload_json`, `attempted_at`, `status`, `response_code`, `attempt_n` |
| `api_tokens` | Service tokens for Cairn (and any future backends) | `name`, `token_hash` (argon2id), `prefix` (first 8 chars for UI), `scopes text[]`, `kind` (`service`), `last_used_at`, `revoked_at NULL` |
| `token_audit_log` | Append-only auth+action log | `token_id`, `passthrough_actor_id NULL`, `passthrough_actor_label NULL`, `action`, `path`, `method`, `ip`, `status_code`, `at` |
| `users` | Humans | `email` (unique), `display_name`, `oidc_sub`, `oidc_issuer`, `last_login_at`. Unique index `(oidc_issuer, oidc_sub)`. |
| `user_sessions` | Browser sessions for the SPA | `user_id`, `cookie_hash`, `created_at`, `expires_at`, `ip`, `user_agent` |
| `activity` | Append-only timeline per issue | `issue_id`, `actor_type` (user/service/passthrough), `actor_id`, `actor_label NULL`, `verb`, `before_json NULL`, `after_json NULL`, `at` |

### Key data-model rules

- **`description_md` is the only description column.** HTML is rendered for display (server or client; not stored).
- **Epics are first-class**, not a label. One issue belongs to at most one epic.
- **Workflows are FK-constrained.** Invalid state transitions are rejected at the API layer, not just discouraged.
- **`issue_intents` is Waypoint-side** — Cairn files an intent at lock-acquire and references the resulting `intent_id` at release.
- **`activity` and `token_audit_log` are two separate append-only logs.** Activity records *what changed about an issue*; audit records *which authenticated principal hit the API*. They serve different audiences (humans browsing issue history vs. the admin audit page).
- **Sequence IDs from Plane are preserved exactly.** `WAY-1` in Plane stays `WAY-1` in Waypoint; no link rot in commit messages or chat.

## API surface

Three layers: REST HTTP (humans + Cairn), no MCP (deliberate — see Scope decision #6), Webhooks (outbound).

### REST partitioning

Same controller logic served on both ports; auth pipeline differs.

```
PUBLIC (:8080 — humans via OIDC, cookie session)
  /                              SPA static assets (via waypoint-web)
  /auth/login, /auth/callback, /auth/logout
  /api/v1/whoami
  /api/v1/projects[/...]         full CRUD on projects/issues/epics/cycles/states/labels/components/issue-types/workflows
  /api/v1/comments/{id}          edit/delete
  /api/v1/attachments/{id}       download
  /api/v1/search
  /api/v1/webhooks[/...]         subscription management
  /api/admin/tokens[/...]        service-token management (admin scope)
  /api/admin/audit               token_audit_log viewer (admin scope)

INTERNAL (:8081 — Cairn only, bearer + NetworkPolicy)
  /internal/v1/projects/{slug}/issues[/...]
  /internal/v1/projects/{slug}/issues/{seq}/transitions
  /internal/v1/projects/{slug}/issues/{seq}/comments
  /internal/v1/projects/{slug}/issues/{seq}/links
  /internal/v1/projects/{slug}/intents       — file/release
  /internal/v1/projects/{slug}/epics
  /internal/v1/search
  /internal/v1/projects                       — list for Cairn's project sync
```

Resources existing on both surfaces use the same controllers and same DB; the auth pipeline differentiates principals. `auth/*` and `admin/*` are public-only; `intents/*` is internal-only.

### Cross-cutting conventions

| Concern | Rule |
|---|---|
| **Public auth** | OIDC against Authelia. SPA → `/auth/login` → 302 to Authelia `/api/oidc/authorize` → callback → `id_token` validated against Authelia's JWKS → `users` upsert → `waypoint_session` cookie issued (HttpOnly, Secure, SameSite=Lax, 30d). |
| **Internal auth** | `Authorization: Bearer wpt_<prefix>_<secret>` (argon2id-verified against `api_tokens.token_hash`, `kind=service`, not revoked). `X-On-Behalf-Of` + `X-On-Behalf-Of-Label` headers trusted because the principal is itself a service token. |
| **Path/cookie/bearer mismatch** | Public rejects bearer tokens → `401 not_for_public_api`. Internal rejects cookies → `401 not_for_internal_api`. Surfaces are mutually exclusive in what they accept. |
| **Scopes** | `[RequireScope("issue:transition")]` attributes. Scope names: `resource:verb`. `*` = master. Cookie sessions get role-derived scopes; service tokens carry stored scopes; passthrough headers do NOT add scopes (service token's scopes are the ceiling). |
| **Error shape** | `{ "error": { "code": "snake_case", "message": "human", "details": {...}? }, "request_id": "uuid" }`. HTTP: 400 / 401 / 403 (with `details.required` = missing scope) / 404 / 409 (invalid state transition) / 422 / 429 (with `Retry-After`). |
| **Idempotency** | Writes accept `Idempotency-Key: <uuid>`. Server stores `(principal_id, key) → (status, body)` for 24h. Replays return cached response. |
| **Pagination** | Cursor-based. `?limit=50&cursor=...` (max 200). Cursor = opaque base64 of `(sort_value, id)`. Responses: `{ data: [...], next_cursor: "...", total_count: N }`. |
| **Request IDs** | `X-Request-Id` header echoed on every response; logged on the API side and in `token_audit_log`. |
| **Rate limits** | None enforced inside Waypoint. Cairn enforces per-agent rate limits before calling Waypoint. Defense-in-depth limit at Traefik for the public surface (per-IP, anti-DoS only). |
| **Content type** | `application/json` except attachment up/download. All descriptions/comments are markdown — no `_html` fields exist. |
| **Versioning** | URL-prefixed `/api/v1/`, `/internal/v1/`. Breaking changes mint `/v2/`. |

### Webhooks

Outbound events with `resource.verb` naming: `issue.created`, `issue.updated`, `issue.transitioned`, `issue.archived`, `issue.deleted`, `comment.created`, `comment.updated`, `comment.deleted`, `epic.created`, `epic.updated`, `epic.closed`, `cycle.started`, `cycle.completed`, `intent.filed`, `intent.released`.

Subscriptions in `webhook_subscriptions`. Delivery loop is an `IHostedService` inside `Waypoint.Api`: picks up `pending` deliveries, POSTs with `X-Waypoint-Signature: sha256=<hmac(secret, body)>`, retries at 1m/5m/30m/2h/12h, then dead-letter. Cairn's existing webhook receiver subscribes here.

## Auth — modular store

### `IPrincipalResolver` abstraction

```csharp
public interface IPrincipalResolver
{
    Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct);
}

public sealed record Principal(
    PrincipalKind Kind,
    string Id,
    string DisplayName,
    IReadOnlyList<string> Scopes,
    string? PassthroughActorId = null,
    string? PassthroughActorLabel = null);

public enum PrincipalKind { Human, InternalService }
```

Each endpoint group registers exactly one resolver in its DI branch. Controllers, audit logging, and scope checks depend only on `Principal` — never on how it was resolved.

### Public resolver — OIDC

Default impl: `OidcSessionResolver`. OAuth 2.0 Authorization Code + PKCE against Authelia.

Configuration (the swap point):

```yaml
auth:
  provider: oidc
  oidc:
    discovery_url: https://auth.chris.box/.well-known/openid-configuration
    client_id: waypoint
    client_secret_ref: { secretName: waypoint-oidc, key: client_secret }
    redirect_uri: https://waypoint.chris.box/auth/callback
    scopes: [openid, profile, email, groups]
    jwks_cache_ttl: 1h
    email_claim: email
    name_claim: name
    groups_claim: groups
    admin_groups: [waypoint-admins]
```

Swap providers (Keycloak, Auth0, Google, custom) by changing this YAML. Swap auth mechanism entirely (LDAP, magic-link, mTLS) by writing a new `IPrincipalResolver` and re-binding the public-endpoint DI registration. Everything downstream is unaffected.

### Internal resolver — service bearer

Default impl: `ServiceBearerResolver`. Validates `Authorization: Bearer wpt_<prefix>_<secret>` via argon2id check against `api_tokens.token_hash`. Returns `Principal{Kind=InternalService, PassthroughActorId=..., PassthroughActorLabel=...}`.

### Service token bootstrap

```bash
kubectl -n media exec deploy/waypoint-api -- \
  dotnet Waypoint.Api.dll seed-token \
    --label cairn-svc --kind service --scopes '*'
# prints: wpt_a1b2c3d4_<long-secret>   (printed once, never stored in plaintext)
```

Token goes into a K8s Secret that Cairn mounts. Ongoing token management via the SPA's `/admin/tokens` page.

## Migration

Standalone .NET console app `src/Waypoint.Importer/`. Two-phase, both idempotent.

### Phase 1 — Dump

```bash
dotnet Waypoint.Importer.dll dump \
  --plane-base https://plane.chris.box/api/v1 \
  --plane-key $PLANE_API_KEY \
  --workspace casa-christo \
  --out ./plane-dump/
```

Produces a directory tree of raw JSON: `workspace.json`, `projects.json`, `projects/{plane_id}/{states,labels,members,issues,cycles,modules}.json`, `projects/{plane_id}/issues/{issue_id}/{activities,comments,attachments/...}.json`, and a `_dump_manifest.json` with timestamps + counts. Pure read; Plane is unaware. Can be re-run any time.

### Phase 2 — Load

```bash
dotnet Waypoint.Importer.dll load \
  --dump ./plane-dump/ \
  --waypoint-db "Host=...;Database=waypoint;..." \
  --mode dry-run        # or: --mode execute
```

Mapping rules:

| Plane | Waypoint | Notes |
|---|---|---|
| `description_html` | `description_md` | Reverse-markdown lib; lossy paths flagged in report |
| Label `type:*` | `issue_types` + `issues.issue_type_id` | Reconstructs first-class types from label hack |
| Label `epic:*` | `epics` + `issues.epic_id` | Reconstructs first-class epics from label hack |
| Other labels | `labels` + `issue_labels` | Direct |
| Plane state | `states`, group preserved | backlog/unstarted/started/completed/cancelled |
| Plane module | `components` + `issue_components` | Module owner → component owner |
| Plane activity | `activity` row | Best-effort verb mapping; unknowns → `legacy_plane_<name>` (lossless) |
| Plane attachment | downloaded → re-uploaded to Waypoint storage; URLs rewritten in descriptions | |
| Plane `sequence_id` | preserved exactly | `WAY-1` stays `WAY-1` |

Each project gets a default workflow seeded (initial permissive shape allowing any-state → any-state). Tightening transitions happens after cutover.

Dry-run output: row counts, sample MD diffs for 5 random issues per project, anomaly list, estimated load time. Run repeatedly while fixing edge cases until clean. Then execute.

### Cutover plan — single maintenance window (~1 hr)

| T | Action |
|---|---|
| T-7d | First full-prod dry-run. Fix issues over the week. |
| T-1h | Announce maintenance. Stop active work. |
| T-0 | `pg_dump` of Plane (kept 30d cold). Fresh `--dump`. Final dry-run, must be clean. |
| T+5m | `--mode execute` against production Waypoint DB. Stops on first error. |
| T+20m | Verify row counts match dump. Browse 5 issues across projects in the new SPA. |
| T+25m | Deploy Cairn with `IIssueTracker → WaypointClient` (one DI registration line — payoff of Plane ticket #2). |
| T+30m | Smoke test from an agent via Cairn MCP: create issue → confirm in Waypoint SPA. Transition → confirm. |
| T+45m | `helm uninstall plane`. PVC retained 30d as `plane-pvc-retired-<date>`. |
| T+60m | Maintenance over. `pg_dump` kept in object storage 30d as cold DR backup. |

No live Plane fallback. Recovery path: if Waypoint loses data in week 1, restore Plane DB to a temporary instance and re-run importer.

## Deployment

| Resource | Count | Notes |
|---|---|---|
| Deployments | 2 | `waypoint-api`, `waypoint-web` |
| StatefulSets | 1 | `waypoint-postgres` (single pod, 50Gi PVC, exporter sidecar) |
| Services | 4 | public, internal, web, postgres |
| IngressRoutes | 1 | public only |
| NetworkPolicies | 2 | api restriction, postgres restriction |
| CronJobs | 1 | nightly pg_dump → MinIO |
| Helm hooks | 1 | `pre-upgrade` migration Job |

Images built multi-stage:
- `Waypoint.Api`: distroless runtime, ~30MB
- `waypoint-web`: Next.js standalone, node:22-alpine, ~150MB

Pushed to `gitea.chris.box/chris/waypoint-{api,web}:vN` via Gitea Actions or build script (whichever matches your other services' pattern).

Secrets (`waypoint-config` K8s Secret): `oidc-client-secret`, `postgres-password`, `postgres-readonly-password` (backup), `service-token-seed` (consumed by CLI on first deploy).

Observability: Serilog JSON → stdout → aggregator. Prometheus metrics on `/metrics` (port 8080, scrape via in-cluster Prometheus — NetworkPolicy clause added for monitoring namespace if applicable). Custom metrics: webhook delivery success/fail, token use, importer progress.

Cairn-side change for cutover (not in this repo):
- New Secret `waypoint-service-token`
- DI swap `IIssueTracker` → `WaypointClient`
- Cairn Deployment must have `app: cairn-api` label (NetworkPolicy match)

## Testing

| Layer | Purpose | Tech | Cadence |
|---|---|---|---|
| **Unit** | Pure logic | xUnit + FluentAssertions | every push, <1s |
| **Architecture** | Module boundary rules (see below) | NetArchTest | every push, <5s |
| **Contract** | `Waypoint.Contracts` JSON shape stability | Verify.Xunit snapshots | every push, <5s |
| **Integration — API** | Real HTTP + Postgres + auth pipeline (two `WebApplicationFactory`s in parallel) | xUnit + Testcontainers.PostgreSql | every PR, ~30s |
| **Integration — importer** | Golden Plane dumps → expected DB state | xUnit + Testcontainers | every PR, ~20s |
| **E2E — SPA** | Playwright critical flows: OIDC login (stubbed Authelia), create/edit/transition/timeline | Playwright + docker compose | every PR, ~2min |
| **Burst load** | 20 concurrent `create_issue` against `:8081`, p95 < 200ms, **zero 429s** | k6 | every PR + nightly, ~30s |
| **Mutation** | Verify tests kill mutations, not just execute lines | Stryker.NET + StrykerJS | nightly + on PR for changed projects, 10-25min |

### Architecture rules (NetArchTest)

```
Waypoint.Domain               must not reference   Waypoint.Api
Waypoint.Api                  must not reference   Waypoint.Importer
Waypoint.Api.Endpoints.Public   must not reference  ServiceBearerResolver
Waypoint.Api.Endpoints.Internal must not reference  OidcSessionResolver
Controllers                   must use             IIssueRepository (not Waypoint.Domain.WaypointDbContext directly)
Waypoint.Contracts            must not reference   EF Core or any non-System.* package
```

The resolver rules enforce the surface split at build time: a public endpoint cannot even *see* the service-bearer resolver type, so it cannot accidentally accept service tokens. Same in reverse for internal endpoints. Folder/namespace convention (`Endpoints/Public/*`, `Endpoints/Internal/*`) carries the policy.

Complemented by **runtime integration tests**: each public endpoint test hits the endpoint with a service bearer token and asserts `401`; each internal endpoint test hits with a cookie and asserts `401`. The static rules prove the bad coupling cannot compile; the runtime tests prove the rejections actually happen.

The last rule (`Contracts` deps) protects Cairn's NuGet consumption from accidental transitive deps.

### Mutation testing scope and gates

| Project | Tested? | Min mutation score | Notes |
|---|---|---|---|
| `Waypoint.Domain` | yes | **85%** | Highest-signal target — pure logic, workflow transitions, scope checks |
| `Waypoint.Api` (scoped to `/Auth`, `/Endpoints`, `/Middleware`) | yes | **80%** | Auth pipeline + endpoint logic; DI bootstrap excluded |
| `Waypoint.Importer` (scoped to `Mapping/`) | yes | **80%** | Mapping rules — silent corruption risk |
| `Waypoint.Contracts` | no | — | DTOs only |
| `Waypoint.Architecture.Tests` | no | — | Tests themselves |
| SPA `web/lib/` (logic) | yes | **70%** | Form validation, URL parsing, state machines |
| SPA `web/components/`, `web/app/` | no | — | Covered by Playwright |

**Hard CI gates** (no override):
- `Waypoint.Domain` mutation score < 85% → PR blocked
- **Any surviving mutation in `Waypoint.Api/Auth/`** → PR blocked unconditionally
- Mutation score regression > 2 points vs. main on any mutation-tested project → PR blocked

Nightly trending in addition to PR gates: slow rot (87→86→85→...) won't be caught by per-PR gates alone.

### OIDC test strategy

No real Authelia in CI. Wiremock-in-Testcontainers serves a fake `/.well-known/openid-configuration` + JWKS; test tokens signed with a test RSA key. Exercises the full OIDC parsing path without Authelia dependency.

## Open items deferred to implementation

These are non-blocking — design doesn't change regardless of choice:

1. Authelia client registration mechanism (manual config edit vs. Helm pre-install hook)
2. Admin bootstrap (`admin_groups` Authelia group vs. first-user-becomes-admin)
3. Whether to match Cairn's "7-tier tests" naming convention — needs a read of Cairn's CI config during implementation; if a deliberate convention exists, align names. Otherwise keep the 8-tier table above.
4. Whether Cairn's `WaypointClient` should retry on 5xx — decided in the Cairn-side ticket that implements the swap; not a Waypoint design concern.
