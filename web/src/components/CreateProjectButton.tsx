'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';

export function CreateProjectButton() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [pending, startTransition] = useTransition();
  const [err, setErr] = useState<string | null>(null);

  async function submit(formData: FormData) {
    setErr(null);
    const body = {
      slug: String(formData.get('slug') || '').trim(),
      name: String(formData.get('name') || '').trim(),
      identifier: String(formData.get('identifier') || '').trim().toUpperCase(),
    };
    const res = await fetch('/api/v1/projects', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      credentials: 'same-origin',
    });
    if (!res.ok) {
      const txt = await res.text();
      setErr(`Failed (${res.status}): ${txt.slice(0, 200)}`);
      return;
    }
    setOpen(false);
    startTransition(() => router.refresh());
  }

  if (!open) {
    return (
      <button
        onClick={() => setOpen(true)}
        className="px-4 py-2 rounded bg-[var(--accent)] text-black font-medium text-sm"
      >
        + New project
      </button>
    );
  }

  return (
    <form
      action={submit}
      className="border border-[var(--border)] rounded p-4 space-y-3"
    >
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <label className="text-sm flex flex-col gap-1">
          <span className="text-[var(--muted)]">Slug (URL)</span>
          <input
            name="slug" required pattern="[a-z0-9\-]+"
            placeholder="my-project"
            className="px-2 py-1 rounded bg-transparent border border-[var(--border)]"
          />
        </label>
        <label className="text-sm flex flex-col gap-1">
          <span className="text-[var(--muted)]">Name</span>
          <input
            name="name" required
            placeholder="My Project"
            className="px-2 py-1 rounded bg-transparent border border-[var(--border)]"
          />
        </label>
        <label className="text-sm flex flex-col gap-1">
          <span className="text-[var(--muted)]">Identifier (3-6 chars)</span>
          <input
            name="identifier" required maxLength={6} pattern="[A-Za-z][A-Za-z0-9]{1,5}"
            placeholder="PROJ"
            className="px-2 py-1 rounded bg-transparent border border-[var(--border)] uppercase"
          />
        </label>
      </div>
      {err && <p className="text-sm text-red-400">{err}</p>}
      <div className="flex gap-2">
        <button
          type="submit" disabled={pending}
          className="px-3 py-1.5 rounded bg-[var(--accent)] text-black text-sm font-medium disabled:opacity-50"
        >
          {pending ? 'Creating…' : 'Create'}
        </button>
        <button
          type="button" onClick={() => { setOpen(false); setErr(null); }}
          className="px-3 py-1.5 rounded border border-[var(--border)] text-sm"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
