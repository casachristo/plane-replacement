'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';

export function CreateIssueButton({ projectSlug }: { projectSlug: string }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [pending, startTransition] = useTransition();
  const [err, setErr] = useState<string | null>(null);

  async function submit(formData: FormData) {
    setErr(null);
    const body = {
      title: String(formData.get('title') || '').trim(),
      descriptionMd: String(formData.get('descriptionMd') || '').trim(),
    };
    const res = await fetch(`/api/v1/projects/${projectSlug}/issues`, {
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
        className="px-3 py-1.5 rounded bg-[var(--accent)] text-[var(--accent-contrast)] font-medium text-sm"
      >
        + New issue
      </button>
    );
  }

  return (
    <form
      action={submit}
      className="border border-[var(--border)] rounded p-4 space-y-3"
    >
      <label className="text-sm flex flex-col gap-1">
        <span className="text-[var(--muted)]">Title</span>
        <input
          name="title" required
          placeholder="Issue title"
          className="px-2 py-1 rounded bg-transparent border border-[var(--border)]"
        />
      </label>
      <label className="text-sm flex flex-col gap-1">
        <span className="text-[var(--muted)]">Description (Markdown)</span>
        <textarea
          name="descriptionMd" rows={5}
          placeholder="What needs to happen, why, acceptance criteria…"
          className="px-2 py-1 rounded bg-transparent border border-[var(--border)] font-mono text-sm"
        />
      </label>
      {err && <p className="text-sm text-red-400">{err}</p>}
      <div className="flex gap-2">
        <button
          type="submit" disabled={pending}
          className="px-3 py-1.5 rounded bg-[var(--accent)] text-[var(--accent-contrast)] text-sm font-medium disabled:opacity-50"
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
