'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import type { State } from '@/lib/api';

export function TransitionMenu({
  projectSlug,
  issueSeq,
  currentStateId,
  states,
}: {
  projectSlug: string;
  issueSeq: number;
  currentStateId: string;
  states: State[];
}) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [err, setErr] = useState<string | null>(null);
  // 412 from the WAY-4 gate; user is prompted to confirm + give a reason.
  const [override, setOverride] = useState<{
    toStateId: string;
    toStateName: string;
    unchecked: { id: string; position: number; text: string }[];
  } | null>(null);

  const endpoint = `/api/v1/projects/${projectSlug}/issues/${issueSeq}/transitions`;

  async function transition(toStateId: string, toStateName: string, force = false, reason?: string) {
    setErr(null);
    const body: Record<string, unknown> = { toStateId };
    if (force) {
      body.force = true;
      body.bypassReason = reason ?? '';
    }
    const res = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      credentials: 'same-origin',
    });
    if (res.status === 412) {
      // WAY-4 gate fired; surface the unchecked items + offer force-override.
      const data = await res.json().catch(() => null);
      const unchecked = data?.error?.details?.unchecked ?? [];
      setOverride({ toStateId, toStateName, unchecked });
      return;
    }
    if (!res.ok) {
      setErr(`Failed (${res.status})`);
      return;
    }
    setOverride(null);
    startTransition(() => router.refresh());
  }

  async function confirmOverride(formData: FormData) {
    if (!override) return;
    const reason = String(formData.get('reason') || '').trim();
    if (!reason) { setErr('Reason required to override.'); return; }
    await transition(override.toStateId, override.toStateName, true, reason);
  }

  const targets = states.filter(s => s.id !== currentStateId);

  return (
    <section className="space-y-3">
      <h2 className="text-lg font-medium">Move to</h2>
      <div className="flex flex-wrap gap-2">
        {targets.map(s => (
          <button
            key={s.id}
            onClick={() => transition(s.id, s.name)}
            disabled={pending}
            className="px-3 py-1 text-sm rounded border border-[var(--border)] hover:border-[var(--accent)] disabled:opacity-50"
          >
            <span
              aria-hidden
              className="w-2 h-2 rounded-full inline-block mr-2 align-middle"
              style={{ background: s.color }}
            />
            {s.name}
          </button>
        ))}
      </div>
      {err && <p className="text-xs text-red-400">{err}</p>}

      {override && (
        <form
          action={confirmOverride}
          className="border border-yellow-500/40 rounded p-3 space-y-3 bg-yellow-500/5"
        >
          <div>
            <p className="text-sm font-medium">
              Cannot move to {override.toStateName} — acceptance criteria unchecked
            </p>
            <ul className="mt-1 text-xs text-[var(--muted)] list-disc pl-5 space-y-0.5">
              {override.unchecked.map(u => <li key={u.id}>{u.text}</li>)}
            </ul>
          </div>
          <label className="block text-sm">
            <span className="block text-[var(--muted)] mb-1">
              Override reason (required, audited as GateOverrideEvent)
            </span>
            <input
              name="reason" required autoFocus
              placeholder="why are you closing this anyway?"
              className="w-full px-2 py-1 rounded bg-transparent border border-[var(--border)] text-sm"
            />
          </label>
          <div className="flex gap-2">
            <button
              type="submit" disabled={pending}
              className="px-3 py-1 rounded bg-yellow-500 text-black text-sm font-medium disabled:opacity-50"
            >
              Override and move
            </button>
            <button
              type="button" onClick={() => { setOverride(null); setErr(null); }}
              className="px-3 py-1 rounded border border-[var(--border)] text-sm"
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
