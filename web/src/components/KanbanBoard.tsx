'use client';

import { useMemo, useState } from 'react';
import type { Issue, State } from '@/lib/api';

export function KanbanBoard({
  projectSlug,
  projectIdentifier,
  states,
  issues,
}: {
  projectSlug: string;
  projectIdentifier: string;
  states: State[];
  issues: Issue[];
}) {
  // No backlog: Backlog-group columns are hidden from the board.
  const visibleStates = useMemo(() => states.filter(s => s.group !== 'Backlog'), [states]);

  // Distinct issue types present, for the filter.
  const types = useMemo(() => {
    const set = new Map<string, string>();
    for (const i of issues) set.set(i.issueTypeId, i.issueTypeName);
    return [...set.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  }, [issues]);

  const [typeFilter, setTypeFilter] = useState<string>('all');

  const shown = useMemo(
    () => (typeFilter === 'all' ? issues : issues.filter(i => i.issueTypeId === typeFilter)),
    [issues, typeFilter],
  );

  const byState = new Map<string, Issue[]>();
  for (const s of visibleStates) byState.set(s.id, []);
  for (const i of shown) byState.get(i.stateId)?.push(i);

  return (
    <div className="space-y-3">
      {types.length > 1 && (
        <div className="flex items-center gap-2 text-sm">
          <span className="text-[var(--muted)]">Type</span>
          <select
            value={typeFilter}
            onChange={e => setTypeFilter(e.target.value)}
            className="rounded border border-[var(--border)] bg-[var(--background)] px-2 py-1"
          >
            <option value="all">All</option>
            {types.map(t => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </select>
        </div>
      )}

      <div className="flex gap-3 overflow-x-auto pb-2">
        {visibleStates.map(s => {
          const items = byState.get(s.id) ?? [];
          return (
            <div
              key={s.id}
              className="flex-shrink-0 w-72 rounded border border-[var(--border)] bg-[var(--background)]"
            >
              <div
                className="px-3 py-2 border-b border-[var(--border)] flex items-center justify-between"
                style={{ borderTopColor: s.color, borderTopWidth: 3 }}
              >
                <div className="flex items-center gap-2 text-sm font-medium">
                  <span
                    aria-hidden
                    className="w-2 h-2 rounded-full inline-block"
                    style={{ background: s.color }}
                  />
                  {s.name}
                </div>
                <span className="text-xs text-[var(--muted)] tabular-nums">{items.length}</span>
              </div>
              <ul className="p-2 space-y-2 min-h-[100px]">
                {items.length === 0 ? (
                  <li className="text-xs text-[var(--muted)] px-2 py-4 text-center">—</li>
                ) : (
                  items.map(i => (
                    <li key={i.id}>
                      <a
                        href={`/projects/${projectSlug}/issues/${i.sequence}`}
                        className="block rounded border border-[var(--border)] p-2 hover:border-[var(--accent)]"
                      >
                        <div className="text-[10px] text-[var(--muted)] tabular-nums">
                          {projectIdentifier}-{i.sequence}
                        </div>
                        <div className="text-sm leading-snug">{i.title}</div>
                      </a>
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
