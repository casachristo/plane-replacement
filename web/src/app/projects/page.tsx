import { listProjects, whoami } from '@/lib/api';
import { CreateProjectButton } from '@/components/CreateProjectButton';

export const dynamic = 'force-dynamic';

export default async function ProjectsPage() {
  const [projects, me] = await Promise.all([listProjects(), whoami()]);
  if (!projects) {
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold">Projects</h1>
        <p className="text-[var(--muted)]">
          You need to sign in to see projects.{' '}
          <a className="underline" href="/auth/login">Sign in</a>
        </p>
      </div>
    );
  }
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Projects</h1>
        {me && <CreateProjectButton />}
      </div>
      {projects.length === 0 ? (
        <p className="text-[var(--muted)]">No projects yet. Create one to get started.</p>
      ) : (
        <ul className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {projects.map(p => (
            <li key={p.id}>
              <a href={`/projects/${p.slug}`}
                 className="block p-4 rounded border border-[var(--border)] hover:border-[var(--accent)]">
                <div className="text-xs text-[var(--muted)]">{p.identifier}</div>
                <div className="font-medium">{p.name}</div>
              </a>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
