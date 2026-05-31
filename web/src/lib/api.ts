// Server-side API helpers. Next.js rewrites /api/v1/* to the Waypoint API host;
// the session cookie is forwarded with the request automatically.

export type Project = {
  id: string;
  slug: string;
  name: string;
  identifier: string;
  createdAt: string;
  updatedAt: string;
};

export type Issue = {
  id: string;
  sequence: number;
  title: string;
  descriptionMd: string;
  stateId: string;
  stateName: string;
  issueTypeId: string;
  issueTypeName: string;
  priority: number;
  createdAt: string;
  updatedAt: string;
};

export type Paged<T> = { data: T[]; nextCursor: string | null; totalCount: number };

async function fetchJson<T>(path: string, init?: RequestInit): Promise<T | null> {
  const res = await fetch(path, { ...init, headers: { 'Accept': 'application/json', ...(init?.headers ?? {}) } });
  if (res.status === 401 || res.status === 404) return null;
  if (!res.ok) throw new Error(`API ${res.status}: ${await res.text()}`);
  return (await res.json()) as T;
}

export async function listProjects(): Promise<Project[] | null> {
  // Public endpoint, returns the projects the human can see.
  return fetchJson<Project[]>('/api/v1/projects');
}

export async function getProject(slug: string): Promise<Project | null> {
  return fetchJson<Project>(`/api/v1/projects/${slug}`);
}

export async function listIssues(slug: string): Promise<Paged<Issue> | null> {
  return fetchJson<Paged<Issue>>(`/api/v1/projects/${slug}/issues?limit=50`);
}
