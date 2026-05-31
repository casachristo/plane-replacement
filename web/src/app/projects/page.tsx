import { listProjects } from '@/lib/api';

export const dynamic = 'force-dynamic';

export default async function ProjectsPage() {
  const projects = await listProjects();
  if (!projects) {
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold">Projects</h1>
        <p className="text-[var(--muted)]">
          You need to sign in to see projects. <a className="underline" href="/auth/login">Sign in</a>
        </p>
      </div>
    );
  }
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Projects</h1>
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
    </div>
  );
}
