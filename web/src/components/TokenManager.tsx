'use client';

import { useState } from 'react';
import type { ApiToken } from '@/lib/api';

const COMMON_SCOPES = ['issue:read', 'issue:create', 'issue:transition', 'comment:create', 'admin'];

export function TokenManager({ initial }: { initial: ApiToken[] }) {
  const [tokens, setTokens] = useState<ApiToken[]>(initial);
  const [name, setName] = useState('');
  const [scopes, setScopes] = useState<string[]>(['issue:read', 'issue:create', 'issue:transition', 'comment:create']);
  const [kind, setKind] = useState<'Service' | 'Admin'>('Service');
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState('');
  const [created, setCreated] = useState<string | null>(null);

  async function refresh() {
    const res = await fetch('/api/admin/tokens/', { cache: 'no-store' });
    if (res.ok) setTokens(await res.json());
  }

  async function create() {
    setErr('');
    if (!name.trim()) { setErr('Name is required'); return; }
    setBusy(true);
    try {
      const res = await fetch('/api/admin/tokens/', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Name: name.trim(), Scopes: scopes, Kind: kind }),
      });
      if (!res.ok) { setErr(`Create failed (${res.status})`); return; }
      const body = await res.json();
      setCreated(body.fullToken);
      setName('');
      await refresh();
    } finally {
      setBusy(false);
    }
  }

  async function revoke(id: string) {
    if (!confirm('Revoke this token? Any agent using it loses access immediately.')) return;
    await fetch(`/api/admin/tokens/${id}`, { method: 'DELETE' });
    await refresh();
  }

  function toggleScope(s: string) {
    setScopes(prev => (prev.includes(s) ? prev.filter(x => x !== s) : [...prev, s]));
  }

  return (
    <div className="space-y-6">
      {created && (
        <div className="rounded border border-[var(--accent)] bg-[var(--background)] p-3 space-y-2">
          <div className="text-sm font-medium">New token — copy it now, it is shown only once:</div>
          <code className="block break-all text-xs bg-black/30 rounded p-2">{created}</code>
          <button className="text-xs text-[var(--muted)] underline" onClick={() => setCreated(null)}>dismiss</button>
        </div>
      )}

      <div className="rounded border border-[var(--border)] p-4 space-y-3">
        <div className="text-sm font-medium">Issue a token (agent credential)</div>
        <div className="flex flex-wrap gap-3 items-end">
          <label className="text-sm">
            <div className="text-[var(--muted)] mb-1">Name</div>
            <input value={name} onChange={e => setName(e.target.value)} placeholder="e.g. cairn-shadow"
              className="rounded border border-[var(--border)] bg-[var(--background)] px-2 py-1" />
          </label>
          <label className="text-sm">
            <div className="text-[var(--muted)] mb-1">Kind</div>
            <select value={kind} onChange={e => setKind(e.target.value as 'Service' | 'Admin')}
              className="rounded border border-[var(--border)] bg-[var(--background)] px-2 py-1">
              <option value="Service">Service</option>
              <option value="Admin">Admin</option>
            </select>
          </label>
        </div>
        <div className="text-sm">
          <div className="text-[var(--muted)] mb-1">Scopes (permissions)</div>
          <div className="flex flex-wrap gap-3">
            {COMMON_SCOPES.map(s => (
              <label key={s} className="flex items-center gap-1.5 text-xs font-mono">
                <input type="checkbox" checked={scopes.includes(s)} onChange={() => toggleScope(s)} />
                {s}
              </label>
            ))}
          </div>
        </div>
        {err && <p className="text-sm text-red-400">{err}</p>}
        <button onClick={create} disabled={busy}
          className="px-4 py-2 rounded bg-[var(--accent)] text-black font-medium disabled:opacity-50">
          {busy ? 'Creating…' : 'Create token'}
        </button>
      </div>

      <div className="space-y-2">
        <div className="text-sm font-medium">Tokens</div>
        {tokens.length === 0 ? (
          <p className="text-[var(--muted)] text-sm">No tokens yet.</p>
        ) : (
          <table className="w-full text-sm">
            <thead className="text-[var(--muted)] text-left text-xs">
              <tr>
                <th className="py-1">Name</th><th>Prefix</th><th>Kind</th><th>Scopes</th><th>Status</th><th></th>
              </tr>
            </thead>
            <tbody>
              {tokens.map(t => (
                <tr key={t.id} className="border-t border-[var(--border)]">
                  <td className="py-2">{t.name}</td>
                  <td className="font-mono text-xs">{t.prefix}…</td>
                  <td>{t.kind}</td>
                  <td className="text-xs font-mono">{t.scopes.join(', ')}</td>
                  <td>{t.revokedAt ? <span className="text-red-400">revoked</span> : <span className="text-green-400">active</span>}</td>
                  <td className="text-right">
                    {!t.revokedAt && (
                      <button onClick={() => revoke(t.id)} className="text-xs text-red-400 underline">revoke</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
