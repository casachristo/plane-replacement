# Waypoint Webhooks

Waypoint emits webhooks for issue, acceptance-criteria, gate-override and comment events.
Every payload is **self-describing**: state-bearing events carry the state `id`, `name`, AND
`group` together (WAY-6), so a subscriber never has to call back into Waypoint just to render
what happened.

## Envelope (v1)

Every delivery body is the same versioned envelope:

```json
{
  "version": 1,
  "event": "issue.transitioned",
  "occurred_at": "2026-06-10T21:50:00.000Z",
  "project_id": "8c1d...e2",
  "payload": { /* event-specific, see below */ }
}
```

| Field         | Type             | Notes                                                        |
|---------------|------------------|-------------------------------------------------------------|
| `version`     | int              | Envelope schema version. Bumped only on a breaking change.  |
| `event`       | string           | Wire event name (see table). Also sent as `X-Waypoint-Event`. |
| `occurred_at` | RFC3339 datetime | When the event was published.                               |
| `project_id`  | uuid \| null     | Owning project; `null` for workspace-wide events.           |
| `payload`     | object           | Event-specific. Keys are `snake_case`; null keys omitted.   |

### Delivery headers

| Header                   | Meaning                                                              |
|--------------------------|---------------------------------------------------------------------|
| `X-Waypoint-Event`       | The wire event name (mirrors `event`).                              |
| `X-Waypoint-Delivery-Id` | Unique id for this delivery attempt (for idempotent receivers).      |
| `X-Waypoint-Signature`   | `HMAC-SHA256(subscription_secret, raw_body)`, hex. Verify before trusting. |

Deliveries are retried with backoff (1m, 5m, 30m, 2h, 12h) and then dead-lettered.

## Events

| Wire name                              | `payload` shape                                  |
|----------------------------------------|--------------------------------------------------|
| `issue.created`                        | `{ issue, state }`                               |
| `issue.updated`                        | `{ issue, ... }`                                 |
| `issue.transitioned`                   | `{ issue, previous_state, new_state }`           |
| `issue.deleted`                        | `{ issue }`                                       |
| `issue.acceptance_criterion.created`   | `{ issue, acceptance_criterion }`                |
| `issue.acceptance_criterion.updated`   | `{ issue, acceptance_criterion }`                |
| `issue.acceptance_criterion.checked`   | `{ issue, acceptance_criterion }`                |
| `issue.acceptance_criterion.unchecked` | `{ issue, acceptance_criterion }`                |
| `issue.acceptance_criterion.deleted`   | `{ issue, acceptance_criterion }`                |
| `gate.override_fired`                  | `{ issue, gate_name, reason, actor_*, at }`      |
| `comment.created`                      | `{ issue, comment }`                             |
| `worklist.current_advanced`            | `{ project, previous_current, new_current, state, remaining_count, done_count, skipped_count, trigger, reason? }` |

### `state` / `previous_state` / `new_state` (WAY-6)

```json
{ "id": "f0e1...9a", "name": "In Progress", "group": "Started" }
```

`group` is one of `Unstarted`, `Started`, `Completed`, `Cancelled` (Cairn-compatible
TrackerStates taxonomy).

### `issue`

```json
{ "id": "a1b2...", "sequence": 42, "title": "Fix the thing" }
```

### `acceptance_criterion`

```json
{
  "id": "c3d4...",
  "position": 1,
  "text": "All tests pass",
  "checked": true,
  "checked_at": "2026-06-10T21:49:00Z",
  "checked_by_actor_type": "User",
  "checked_by_actor_id": "1111...1111",
  "checked_by_actor_label": "Test User"
}
```

When unchecked, the `checked_*` fields are `null` / `false`. `checked_by_actor_*` matches the
WAY-3 audit actor fields (`type`, `id`, `label`).

### `gate.override_fired` (WAY-9)

```json
{
  "issue": { "id": "...", "sequence": 42, "title": "..." },
  "gate_name": "acceptance_criteria_unchecked",
  "reason": "shipping; follow-up filed",
  "actor_type": "User",
  "actor_id": "1111...1111",
  "actor_label": "Test User",
  "at": "2026-06-10T21:49:30Z"
}
```

## Rendering a transition with zero callbacks (WAY-6)

Because both states are inlined, a trivial subscriber renders the move with no auth and no
round-trip back to Waypoint — just `curl` + `jq`:

```bash
# Pipe a delivery body straight into jq:
jq -r '"\(.payload.issue.title): \(.payload.previous_state.name) → \(.payload.new_state.name)"'
# => Fix the thing: To Do → Done
```

Or as a one-line receiver:

```bash
# nc/socat receiver feeding jq; no API key, no lookup of the state UUID.
jq -r '.payload | "#\(.issue.sequence) \(.issue.title): \(.previous_state.name) (\(.previous_state.group)) → \(.new_state.name) (\(.new_state.group))"'
```
