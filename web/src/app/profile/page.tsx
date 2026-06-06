import { whoami } from '@/lib/api';

export const dynamic = 'force-dynamic';

export default async function ProfilePage() {
  const me = await whoami();

  if (!me) {
    return (
      <div className="space-y-3">
        <h1 className="text-2xl font-semibold">Profile</h1>
        <p className="text-[var(--muted)]">You are not signed in.</p>
        <a href="/auth/login" className="inline-block px-4 py-2 rounded border border-[var(--border)]">
          Sign in
        </a>
      </div>
    );
  }

  const isAdmin = me.scopes.includes('admin');

  return (
    <div className="space-y-6 max-w-2xl">
      <div className="flex items-center gap-3">
        <h1 className="text-2xl font-semibold">{me.displayName}</h1>
        {isAdmin && (
          <span className="px-2 py-0.5 rounded text-xs bg-[var(--accent)] text-black font-medium">admin</span>
        )}
      </div>

      <dl className="grid grid-cols-[8rem_1fr] gap-2 text-sm">
        <dt className="text-[var(--muted)]">Identity</dt>
        <dd>{me.id}</dd>
        <dt className="text-[var(--muted)]">Type</dt>
        <dd>{me.kind === 'Human' ? 'Person' : 'Service'}</dd>
      </dl>

      <div className="space-y-2">
        <div className="text-sm text-[var(--muted)]">Permissions (scopes)</div>
        <div className="flex flex-wrap gap-2">
          {me.scopes.length === 0 ? (
            <span className="text-[var(--muted)] text-sm">none</span>
          ) : (
            me.scopes.map(s => (
              <span key={s} className="px-2 py-0.5 rounded text-xs border border-[var(--border)] font-mono">
                {s}
              </span>
            ))
          )}
        </div>
      </div>

      {isAdmin && (
        <div className="pt-2 border-t border-[var(--border)] space-y-2">
          <div className="text-sm text-[var(--muted)]">Admin</div>
          <a href="/admin/tokens" className="inline-block px-4 py-2 rounded border border-[var(--border)]">
            Agent permissions (API tokens)
          </a>
        </div>
      )}

      <form action="/auth/logout" method="post">
        <button className="px-4 py-2 rounded border border-[var(--border)] text-[var(--muted)]" type="submit">
          Sign out
        </button>
      </form>
    </div>
  );
}
