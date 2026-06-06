'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import type { Issue, State, Epic } from '@/lib/api';

const NO_MODULE = '__none__';

export function KanbanBoard({
  projectSlug,
  projectIdentifier,
  states,
  epics,
  issues,
}: {
  projectSlug: string;
  projectIdentifier: string;
  states: State[];
  epics: Epic[];
  issues: Issue[];
}) {
  const router = useRouter();
  const [groupBy, setGroupBy] = useState<'state' | 'module'>('state');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [pending, setPending] = useState<string | null>(null);

  // No backlog: Backlog-group columns are hidden when grouping by state.
  const visibleStates = useMemo(() => states.filter(s => s.group !== 'Backlog'), [states]);
  const stateName = useMemo(() => new Map(states.map(s => [s.id, s.name])), [states]);

  const types = useMemo(() => {
    const set = new Map<string, string>();
    for (const i of issues) set.set(i.issueTypeId, i.issueTypeName);
    return [...set.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  }, [issues]);

  const shown = useMemo(
    () => (typeFilter === 'all' ? issues : issues.filter(i => i.issueTypeId === typeFilter)),
    [issues, typeFilter],
  );

  // Columns depend on the grouping dimension.
  const columns =
    groupBy === 'state'
      ? visibleStates.map(s => ({ key: s.id, title: s.name, color: s.color }))
      : [
          ...epics.map(e => ({ key: e.id, title: e.title, color: '#a855f7' })),
          { key: NO_MODULE, title: 'No module', color: '#64748b' },
        ];

  const bucketKey = (i: Issue) => (groupBy === 'state' ? i.stateId : i.epicId ?? NO_MODULE);

  const byColumn = new Map<string, Issue[]>();
  for (const c of columns) byColumn.set(c.key, []);
  for (const i of shown) byColumn.get(bucketKey(i))?.push(i);

  async function assignModule(seq: number, epicKey: string) {
    setPending(`${seq}`);
    try {
      await fetch(`/api/v1/projects/${projectSlug}/issues/${seq}/epic`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ EpicId: epicKey === NO_MODULE ? null : epicKey }),
      });
      router.refresh();
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-4 text-sm">
        <div className="flex items-center gap-2">
          <span className="text-[var(--muted)]">Group by</span>
          <div className="inline-flex rounded border border-[var(--border)] overflow-hidden">
            {(['state', 'module'] as const).map(g => (
              <button
                key={g}
                onClick={() => setGroupBy(g)}
                className={`px-2 py-1 capitalize ${groupBy === g ? 'bg-[var(--accent)] text-black' : ''}`}
              >
                {g}
              </button>
            ))}
          </div>
        </div>
        {types.length > 1 && (
          <div className="flex items-center gap-2">
            <span className="text-[var(--muted)]">Type</span>
            <select
              value={typeFilter}
              onChange={e => setTypeFilter(e.target.value)}
              className="rounded border border-[var(--border)] bg-[var(--background)] px-2 py-1"
            >
              <option value="all">All</option>
              {types.map(t => (
                <option key={t.id} value={t.id}>{t.name}</option>
              ))}
            </select>
          </div>
        )}
      </div>

      {groupBy === 'module' && epics.length === 0 && (
        <p className="text-xs text-[var(--muted)]">
          No modules yet — every issue is under “No module”. Create modules with POST
          <code className="mx-1">/api/v1/projects/{projectSlug}/epics</code>, then assign issues from a card.
        </p>
      )}

      <div className="flex gap-3 overflow-x-auto pb-2">
        {columns.map(col => {
          const items = byColumn.get(col.key) ?? [];
          return (
            <div key={col.key} className="flex-shrink-0 w-72 rounded border border-[var(--border)] bg-[var(--background)]">
              <div
                className="px-3 py-2 border-b border-[var(--border)] flex items-center justify-between"
                style={{ borderTopColor: col.color, borderTopWidth: 3 }}
              >
                <div className="flex items-center gap-2 text-sm font-medium">
                  <span aria-hidden className="w-2 h-2 rounded-full inline-block" style={{ background: col.color }} />
                  {col.title}
                </div>
                <span className="text-xs text-[var(--muted)] tabular-nums">{items.length}</span>
              </div>
              <ul className="p-2 space-y-2 min-h-[100px]">
                {items.length === 0 ? (
                  <li className="text-xs text-[var(--muted)] px-2 py-4 text-center">—</li>
                ) : (
                  items.map(i => (
                    <li key={i.id} className="rounded border border-[var(--border)] p-2 hover:border-[var(--accent)] space-y-1">
                      <a href={`/projects/${projectSlug}/issues/${i.sequence}`} className="block">
                        <div className="text-[10px] text-[var(--muted)] tabular-nums">
                          {projectIdentifier}-{i.sequence}
                          {groupBy === 'module' && <span className="ml-2">· {stateName.get(i.stateId)}</span>}
                        </div>
                        <div className="text-sm leading-snug">{i.title}</div>
                      </a>
                      <select
                        value={i.epicId ?? NO_MODULE}
                        disabled={pending === `${i.sequence}`}
                        onChange={e => assignModule(i.sequence, e.target.value)}
                        className="w-full text-[11px] rounded border border-[var(--border)] bg-[var(--background)] px-1 py-0.5 text-[var(--muted)]"
                        title="Module"
                      >
                        <option value={NO_MODULE}>No module</option>
                        {epics.map(e => (
                          <option key={e.id} value={e.id}>{e.title}</option>
                        ))}
                      </select>
                    </li>
                  ))
                )}
              </ul>
            </div>
          );
        })}
      </div>
    </div>
  );
}
