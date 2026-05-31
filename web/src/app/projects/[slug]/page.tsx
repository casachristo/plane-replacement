import { getProject, listIssues } from '@/lib/api';

export const dynamic = 'force-dynamic';

export default async function ProjectPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const project = await getProject(slug);
  if (!project) return <div>Project not found or sign-in required.</div>;
  const page = await listIssues(slug);

  return (
    <div className="space-y-6">
      <div>
        <div className="text-xs text-[var(--muted)]">{project.identifier}</div>
        <h1 className="text-2xl font-semibold">{project.name}</h1>
      </div>
      <section>
        <h2 className="text-lg font-medium mb-3">Issues</h2>
        {!page || page.data.length === 0 ? (
          <p className="text-[var(--muted)]">No issues yet.</p>
        ) : (
          <ul className="divide-y divide-[var(--border)] border border-[var(--border)] rounded">
            {page.data.map(i => (
              <li key={i.id} className="px-4 py-3 flex items-center gap-3">
                <span className="text-[var(--muted)] text-sm tabular-nums w-20">
                  {project.identifier}-{i.sequence}
                </span>
                <span className="flex-1">{i.title}</span>
                <span className="text-xs px-2 py-0.5 rounded bg-[var(--border)]">{i.stateName}</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
