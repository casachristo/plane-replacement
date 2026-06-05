import { getProject, getIssue, listStates, whoami } from '@/lib/api';
import { AcceptanceCriteriaPanel } from '@/components/AcceptanceCriteriaPanel';
import { TransitionMenu } from '@/components/TransitionMenu';

export const dynamic = 'force-dynamic';

export default async function IssuePage({
  params,
}: {
  params: Promise<{ slug: string; seq: string }>;
}) {
  const { slug, seq: seqStr } = await params;
  const seq = Number(seqStr);
  if (!Number.isFinite(seq)) return <div>Invalid issue number.</div>;

  const [project, issue, states, me] = await Promise.all([
    getProject(slug),
    getIssue(slug, seq),
    listStates(slug),
    whoami(),
  ]);
  if (!project) return <div>Project not found or sign-in required.</div>;
  if (!issue) return <div>Issue {project.identifier}-{seq} not found.</div>;

  return (
    <div className="space-y-6">
      <div>
        <div className="text-xs text-[var(--muted)]">
          <a href={`/projects/${slug}`} className="hover:text-[var(--foreground)]">
            {project.name}
          </a>
          {' / '}
          <span className="tabular-nums">{project.identifier}-{issue.sequence}</span>
        </div>
        <h1 className="text-2xl font-semibold mt-1">{issue.title}</h1>
        <div className="mt-2">
          <span className="text-xs px-2 py-0.5 rounded bg-[var(--border)]">
            {issue.stateName}
          </span>
        </div>
      </div>

      {issue.descriptionMd && (
        <section>
          <h2 className="text-lg font-medium mb-2">Description</h2>
          <pre className="whitespace-pre-wrap font-sans text-sm leading-relaxed border border-[var(--border)] rounded p-3 bg-[var(--background)]">
            {issue.descriptionMd}
          </pre>
        </section>
      )}

      <AcceptanceCriteriaPanel
        projectSlug={slug}
        issueSeq={seq}
        initial={issue.acceptanceCriteria ?? []}
        canEdit={me !== null}
      />

      {me && states && states.length > 1 && (
        <TransitionMenu
          projectSlug={slug}
          issueSeq={seq}
          currentStateId={issue.stateId}
          states={states}
        />
      )}
    </div>
  );
}
