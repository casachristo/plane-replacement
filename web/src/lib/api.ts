// Server-side API helpers. In the browser, fetch('/api/v1/...') works because Next.js
// rewrites paths to the API host. In server components, fetch needs an absolute URL,
// so we prepend WAYPOINT_API_BASE. The session cookie must be forwarded manually on
// the server side via next/headers — see the cookies() helper.

import { cookies } from 'next/headers';

const SERVER_API_BASE = process.env.WAYPOINT_API_BASE ?? 'http://waypoint-public';

function isServer(): boolean {
  return typeof window === 'undefined';
}

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
  const url = isServer() ? `${SERVER_API_BASE}${path}` : path;
  const headers: Record<string, string> = { 'Accept': 'application/json', ...(init?.headers as Record<string, string> ?? {}) };
  if (isServer()) {
    // Forward the session cookie so the API sees the same human principal.
    const cookieStore = await cookies();
    const session = cookieStore.get('waypoint_session');
    if (session) headers['Cookie'] = `waypoint_session=${session.value}`;
  }
  const res = await fetch(url, { ...init, headers, cache: 'no-store' });
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
