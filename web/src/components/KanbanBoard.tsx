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
  const byState = new Map<string, Issue[]>();
  for (const s of states) byState.set(s.id, []);
  for (const i of issues) {
    const bucket = byState.get(i.stateId);
    if (bucket) bucket.push(i);
    else byState.set(i.stateId, [i]);   // safety: bucket missing if state was deleted
  }

  return (
    <div className="flex gap-3 overflow-x-auto pb-2">
      {states.map(s => {
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
  );
}
