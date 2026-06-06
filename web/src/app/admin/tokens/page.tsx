import { whoami, listTokens } from '@/lib/api';
import { TokenManager } from '@/components/TokenManager';

export const dynamic = 'force-dynamic';

export default async function AdminTokensPage() {
  const me = await whoami();
  if (!me?.scopes.includes('admin')) {
    return (
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold">Agent permissions</h1>
        <p className="text-[var(--muted)]">Admin access required.</p>
      </div>
    );
  }

  const tokens = (await listTokens()) ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Agent permissions</h1>
        <p className="text-[var(--muted)] text-sm max-w-2xl">
          API tokens are the credentials agents (like Cairn) use to call Waypoint. Each token
          carries a set of scopes that bound what it may do; an Admin-kind token additionally
          gets the <code>admin</code> scope.
        </p>
      </div>
      <TokenManager initial={tokens} />
    </div>
  );
}
