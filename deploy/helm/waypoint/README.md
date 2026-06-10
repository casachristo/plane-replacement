# Waypoint Helm chart

Single chart for the full Waypoint stack in K3s.

## Install

```bash
# 1. Create Secrets (see templates/secrets-template.yaml)
kubectl -n media create secret generic waypoint-config --from-literal=...
kubectl -n media create secret generic waypoint-oidc --from-literal=client_secret=...

# 2. Push images
docker build -f src/Waypoint.Api/Dockerfile -t gitea.chris.box/chris/waypoint-api:0.1.0 .
docker push gitea.chris.box/chris/waypoint-api:0.1.0
docker build -f web/Dockerfile -t gitea.chris.box/chris/waypoint-web:0.1.0 web
docker push gitea.chris.box/chris/waypoint-web:0.1.0

# 3. Register Waypoint as an OIDC client in Authelia (configuration.yml):
#    identity_providers.oidc.clients:
#      - id: waypoint
#        secret: '<digest>'                    # matches waypoint-oidc Secret
#        redirect_uris: ['https://waypoint.chris.box/auth/callback']
#        scopes: [openid, profile, email, groups]
#        userinfo_signing_algorithm: none

# 4. Install
helm upgrade --install waypoint deploy/helm/waypoint -n media

# 5. Seed the Cairn service token (one-time)
kubectl -n media exec deploy/waypoint-api -- \
  dotnet Waypoint.Api.dll seed-token --label cairn-svc --kind service --scopes '*'
# Save the printed token into Cairn's secret store.
```

## Portability (WAY-16)

Waypoint owns its own Postgres and has no hard dependency on homelab-specific
infrastructure — it deploys independently to other hardware/cloud by overriding
chart values and providing the externalized Secrets. Verification:

| Dependency       | How it's parameterized                                              | Secret |
|------------------|--------------------------------------------------------------------|--------|
| Postgres         | `ConnectionStrings:Postgres` from `waypoint-config`; in-chart `postgres.*` | `waypoint-config` |
| OIDC provider    | `Oidc:Authority` / `ClientId` / `RedirectUri` (chart `api.oidc.*`)  | `client_secret` in `waypoint-oidc` |
| Ingress host     | `ingress.host`                                                      | TLS in `waypoint-tls` |
| Backup target    | `backup.s3.endpoint` (chart); credentials in `waypoint-config`      | `s3-token` |
| Container images | `api.image` / `web.image` (any registry)                           | n/a |

- **No secrets are baked into `values.yaml` or `appsettings.json`** — every
  credential is sourced from an out-of-band Kubernetes Secret (see
  `templates/secrets-template.yaml`); works with external-secrets / sealed-secrets.
- The only homelab values are convenience fallbacks in app config
  (`Oidc:Authority` defaults to `https://auth.chris.box`, client id `waypoint`).
  A foreign deployment overrides them via `api.oidc.*` chart values — they are not
  required to be homelab values.

To deploy elsewhere: set `ingress.host`, `api.oidc.authority` + `redirectUri`,
point `ConnectionStrings:Postgres` at your DB (or keep the in-chart Postgres),
provide the three Secrets, and push the images to any registry.

## Cutover (Phase 6 — destructive, not automated)

See `docs/superpowers/specs/2026-05-30-waypoint-design.md § Cutover`.
Summary:
1. T-1h: announce maintenance.
2. T-0: `pg_dump` of Plane; final `Waypoint.Importer dump`; final dry-run.
3. T+5m: `--mode execute` against Waypoint DB.
4. T+25m: deploy Cairn with `IIssueTracker → WaypointClient`.
5. T+30m: smoke test via Cairn MCP.
6. T+45m: `helm uninstall plane`. PVC retained 30d.
