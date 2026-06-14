# Waypoint Standard Architecture (agent reference)

This is the canonical structure agents follow when building or refactoring a subsystem.
Waypoint is the reference implementation. The goal is that every layer is a self-contained
unit you can reason about, test, and reassign without tracing state across the codebase.

## Hierarchy

```
Subsystem  >  Module  >  Submodule  >  Feature
```

- **Subsystem** — a bounded slice of the domain (e.g. Issues, Projects, Identity).
- **Module / Submodule** — groupings of related features inside a subsystem.
- **Feature** — the smallest unit of behaviour (e.g. comments, acceptance-criteria, transitions).

A layer is any node in that tree. Leaves are features; everything above is a grouping.

## The three roles every layer is built from

- **Manager — owns state.** Holds and mutates the layer's data (its aggregate / entities).
  It is the *only* thing that touches persistence for that layer. Stateful. Private to the layer.
- **Service — the stateless interface to the Manager.** The public facade other layers call.
  Validates inputs, asks the Manager to change state, maps to results. Holds no state of its own.
  Callers depend on the Service, never on the Manager.
- **Orchestrator — coordinates child Services.** Where a layer has children, the Orchestrator
  sequences calls across the child Services and fires cross-cutting effects (events, audit).
  It holds no state; it composes child Services. **Leaf features omit the Orchestrator** — there
  are no children to coordinate.

A layer **owns its objects**: the entity/aggregate (state, via the Manager) and its operations
(stateless, via the Service) live together as one self-contained unit.

## Dependency rules

1. A parent's **Orchestrator** calls **child Services** (down the tree) — never child Managers.
2. A **Service never calls a sibling's Manager**. Cross-layer work goes through the sibling's
   Service, or up to the parent Orchestrator.
3. **Managers are private** to their layer. Nothing outside the layer references the Manager.
4. State flows through Managers only; everything else is stateless and composable.

## The API layer is exempt

HTTP endpoints stay **thin adapters**: authenticate, resolve the relevant subsystem Service (or
Orchestrator), call one method, map to a DTO. Endpoints contain **no** domain logic, no state,
and never touch a Manager or the DbContext directly. (Plain CRUD endpoints may call a feature
Service directly; anything spanning features goes through the subsystem Orchestrator.)

## Folder convention (Waypoint)

```
src/Waypoint.Api/Subsystems/<Subsystem>/[<Module>/]<Feature>/
    <Feature>Manager.cs      // state  (I<Feature>Manager + impl)
    <Feature>Service.cs      // stateless facade (I<Feature>Service + impl)
<Subsystem>/<Subsystem>Orchestrator.cs   // present only on nodes with children
```

DTO mapping and input validation live in the Service. Persistence + invariants live in the Manager.

## Waypoint subsystem map (proposed)

| Subsystem    | Features (leaf)                                          | Orchestrates |
|--------------|---------------------------------------------------------|--------------|
| **Issues**   | issue-crud, comments, acceptance-criteria, activity     | transition gate (issue + AC + workflow + activity + webhook) |
| **Projects** | project, states, workflow, issue-types, labels          | project provisioning (seed states/workflow/types on create)  |
| **Planning** | epics, cycles, worklist, intents                        | —            |
| **Identity** | tokens, sessions, principals/scopes                     | principal resolution chain |
| **Integration** | webhooks, importer, cairn module source              | webhook dispatch fan-out |

## Migration approach (per the refactor tickets)

Refactor one subsystem at a time, smallest/safest first. For each: introduce the Manager
(move the existing repository's state ops), introduce the Service (move validation + DTO mapping
out of the endpoint), rewire the endpoints to the Service, delete the old repository, **run the
test suite green before moving on**. Mutation testing runs once at the very end of the whole
refactor, not per layer.
