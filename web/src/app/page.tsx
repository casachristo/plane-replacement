export default function HomePage() {
  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold">Waypoint</h1>
      <p className="text-[var(--muted)] max-w-2xl">
        A self-hosted issue tracker that replaces Plane. Built to be agent-friendly:
        the MCP layer lives in Cairn, not here — Waypoint is just a fast, narrow CRUD
        service that Cairn calls over a service-bearer-gated internal endpoint.
      </p>
      <div className="flex gap-3">
        <a href="/projects" className="px-4 py-2 rounded bg-[var(--accent)] text-black font-medium">
          Browse projects
        </a>
        <a href="/auth/login" className="px-4 py-2 rounded border border-[var(--border)]">
          Sign in
        </a>
      </div>
    </div>
  );
}
