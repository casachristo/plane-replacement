import { getProject, listIssues, listStates, whoami } from '@/lib/api';
import { CreateIssueButton } from '@/components/CreateIssueButton';
import { KanbanBoard } from '@/components/KanbanBoard';

export const dynamic = 'force-dynamic';

export default async function ProjectPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const [project, page, states, me] = await Promise.all([
    getProject(slug),
    listIssues(slug),
    listStates(slug),
    whoami(),
  ]);
  if (!project) return <div>Project not found or sign-in required.</div>;

  return (
    <div className="space-y-6">
      <div className="flex items-end justify-between">
        <div>
          <div className="text-xs text-[var(--muted)]">{project.identifier}</div>
          <h1 className="text-2xl font-semibold">{project.name}</h1>
        </div>
        {me && <CreateIssueButton projectSlug={slug} />}
      </div>
      {states && states.length > 0 ? (
        <KanbanBoard
          projectSlug={slug}
          projectIdentifier={project.identifier}
          states={states}
          issues={page?.data ?? []}
        />
      ) : (
        <p className="text-[var(--muted)]">
          No workflow states defined for this project yet.
        </p>
      )}
    </div>
  );
}
