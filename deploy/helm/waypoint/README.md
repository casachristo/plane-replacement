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

## Cutover (Phase 6 — destructive, not automated)

See `docs/superpowers/specs/2026-05-30-waypoint-design.md § Cutover`.
Summary:
1. T-1h: announce maintenance.
2. T-0: `pg_dump` of Plane; final `Waypoint.Importer dump`; final dry-run.
3. T+5m: `--mode execute` against Waypoint DB.
4. T+25m: deploy Cairn with `IIssueTracker → WaypointClient`.
5. T+30m: smoke test via Cairn MCP.
6. T+45m: `helm uninstall plane`. PVC retained 30d.
