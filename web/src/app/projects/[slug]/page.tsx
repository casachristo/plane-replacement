import { getProject, listIssues, whoami } from '@/lib/api';
import { CreateIssueButton } from '@/components/CreateIssueButton';

export const dynamic = 'force-dynamic';

export default async function ProjectPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const [project, page, me] = await Promise.all([
    getProject(slug),
    listIssues(slug),
    whoami(),
  ]);
  if (!project) return <div>Project not found or sign-in required.</div>;

  return (
    <div className="space-y-6">
      <div>
        <div className="text-xs text-[var(--muted)]">{project.identifier}</div>
        <h1 className="text-2xl font-semibold">{project.name}</h1>
      </div>
      <section className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-medium">Issues</h2>
          {me && <CreateIssueButton projectSlug={slug} />}
        </div>
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
