'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import type { AcceptanceCriterion } from '@/lib/api';

export function AcceptanceCriteriaPanel({
  projectSlug,
  issueSeq,
  initial,
  canEdit,
}: {
  projectSlug: string;
  issueSeq: number;
  initial: AcceptanceCriterion[];
  canEdit: boolean;
}) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [err, setErr] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [items, setItems] = useState<AcceptanceCriterion[]>(initial);

  const base = `/api/v1/projects/${projectSlug}/issues/${issueSeq}/acceptance-criteria`;

  async function call(method: string, path: string, body?: object): Promise<Response> {
    return fetch(path, {
      method,
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
      credentials: 'same-origin',
    });
  }

  async function refreshFromServer() {
    const res = await fetch(`/api/v1/projects/${projectSlug}/issues/${issueSeq}`, {
      credentials: 'same-origin',
    });
    if (res.ok) {
      const issue = await res.json();
      setItems(issue.acceptanceCriteria ?? []);
    }
    // Also rerun the server component so other readers see fresh data.
    startTransition(() => router.refresh());
  }

  async function add(formData: FormData) {
    setErr(null);
    const text = String(formData.get('text') || '').trim();
    if (!text) return;
    const res = await call('POST', base, { text });
    if (!res.ok) { setErr(`Failed (${res.status})`); return; }
    setAdding(false);
    await refreshFromServer();
  }

  async function toggle(ac: AcceptanceCriterion) {
    setErr(null);
    const res = await call('POST', `${base}/${ac.id}/${ac.checked ? 'uncheck' : 'check'}`);
    if (!res.ok) { setErr(`Failed (${res.status})`); return; }
    await refreshFromServer();
  }

  async function remove(ac: AcceptanceCriterion) {
    setErr(null);
    const res = await call('DELETE', `${base}/${ac.id}`);
    if (!res.ok) { setErr(`Failed (${res.status})`); return; }
    await refreshFromServer();
  }

  const total = items.length;
  const done = items.filter(a => a.checked).length;

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-medium">
          Acceptance criteria
          {total > 0 && (
            <span className="ml-2 text-sm text-[var(--muted)] tabular-nums">
              {done}/{total}
            </span>
          )}
        </h2>
        {canEdit && !adding && (
          <button
            onClick={() => setAdding(true)}
            className="text-xs px-2 py-1 rounded border border-[var(--border)] hover:border-[var(--accent)]"
          >
            + Add
          </button>
        )}
      </div>

      {items.length === 0 && !adding ? (
        <p className="text-sm text-[var(--muted)]">
          No acceptance criteria yet.
          {canEdit && ' Add one to gate this issue\'s transition to Done.'}
        </p>
      ) : (
        <ul className="space-y-1">
          {items.map(ac => (
            <li key={ac.id} className="flex items-start gap-2 py-1">
              <input
                type="checkbox"
                checked={ac.checked}
                disabled={!canEdit || pending}
                onChange={() => toggle(ac)}
                className="mt-1 cursor-pointer disabled:cursor-not-allowed"
              />
              <span className={`flex-1 text-sm ${ac.checked ? 'text-[var(--muted)] line-through' : ''}`}>
                {ac.text}
              </span>
              {ac.checked && ac.checkedByActorLabel && (
                <span className="text-[10px] text-[var(--muted)] whitespace-nowrap">
                  by {ac.checkedByActorLabel}
                </span>
              )}
              {canEdit && (
                <button
                  onClick={() => remove(ac)}
                  disabled={pending}
                  className="text-xs text-[var(--muted)] hover:text-red-400 disabled:opacity-50"
                  aria-label="Delete acceptance criterion"
                >
                  ×
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {adding && (
        <form action={add} className="flex gap-2">
          <input
            name="text" required autoFocus
            placeholder="e.g. login button is visible after sign-in"
            className="flex-1 px-2 py-1 rounded bg-transparent border border-[var(--border)] text-sm"
          />
          <button
            type="submit" disabled={pending}
            className="px-3 py-1 rounded bg-[var(--accent)] text-[var(--accent-contrast)] text-sm font-medium disabled:opacity-50"
          >
            Add
          </button>
          <button
            type="button" onClick={() => { setAdding(false); setErr(null); }}
            className="px-3 py-1 rounded border border-[var(--border)] text-sm"
          >
            Cancel
          </button>
        </form>
      )}

      {err && <p className="text-xs text-red-400">{err}</p>}
    </section>
  );
}
