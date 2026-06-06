import { whoami } from '@/lib/api';

export const dynamic = 'force-dynamic';

export default async function HomePage() {
  const me = await whoami();
  const isAdmin = me?.scopes.includes('admin') ?? false;

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold">Waypoint</h1>
      <p className="text-[var(--muted)] max-w-2xl">
        A self-hosted issue tracker that replaces Plane. Built to be agent-friendly:
        the MCP layer lives in Cairn, not here — Waypoint is just a fast, narrow CRUD
        service that Cairn calls over a service-bearer-gated internal endpoint.
      </p>

      {me ? (
        <div className="space-y-3">
          <div className="text-sm text-[var(--muted)]">
            Signed in as <span className="text-[var(--foreground)] font-medium">{me.displayName}</span>
            {isAdmin && (
              <span className="ml-2 px-2 py-0.5 rounded text-xs bg-[var(--accent)] text-black font-medium">
                admin
              </span>
            )}
          </div>
          <div className="flex gap-3">
            <a href="/projects" className="px-4 py-2 rounded bg-[var(--accent)] text-black font-medium">
              Browse projects
            </a>
            <a href="/profile" className="px-4 py-2 rounded border border-[var(--border)]">
              Profile
            </a>
          </div>
        </div>
      ) : (
        <div className="flex gap-3">
          <a href="/projects" className="px-4 py-2 rounded bg-[var(--accent)] text-black font-medium">
            Browse projects
          </a>
          <a href="/auth/login" className="px-4 py-2 rounded border border-[var(--border)]">
            Sign in
          </a>
        </div>
      )}
    </div>
  );
}
