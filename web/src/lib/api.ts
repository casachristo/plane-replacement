// Server-side API helpers. In the browser, fetch('/api/v1/...') works because Next.js
// rewrites paths to the API host. In server components, fetch needs an absolute URL,
// so we prepend WAYPOINT_API_BASE. The session cookie must be forwarded manually on
// the server side via next/headers — see the cookies() helper.

import { cookies, headers as incomingHeaders } from 'next/headers';

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

export type AcceptanceCriterion = {
  id: string;
  position: number;
  text: string;
  checked: boolean;
  checkedAt: string | null;
  checkedByActorType: string | null;
  checkedByActorId: string | null;
  checkedByActorLabel: string | null;
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
  acceptanceCriteria?: AcceptanceCriterion[];
};

export type State = {
  id: string;
  name: string;
  group: 'Backlog' | 'Unstarted' | 'Started' | 'Completed' | 'Cancelled';
  color: string;
  sortOrder: number;
  isDefault: boolean;
};

export type Paged<T> = { data: T[]; nextCursor: string | null; totalCount: number };

export type Me = {
  kind: 'Human' | 'InternalService';
  id: string;
  displayName: string;
  scopes: string[];
};

async function fetchJson<T>(path: string, init?: RequestInit): Promise<T | null> {
  const url = isServer() ? `${SERVER_API_BASE}${path}` : path;
  const headers: Record<string, string> = { 'Accept': 'application/json', ...(init?.headers as Record<string, string> ?? {}) };
  if (isServer()) {
    const cookieStore = await cookies();
    const session = cookieStore.get('waypoint_session');
    if (session) headers['Cookie'] = `waypoint_session=${session.value}`;
    // Forward the Authelia forward-auth identity headers from the incoming request so the
    // API's AutheliaHeaderResolver recognizes the SSO'd user without a separate Waypoint login.
    const inc = await incomingHeaders();
    for (const name of ['remote-email', 'remote-name', 'remote-user', 'remote-groups']) {
      const v = inc.get(name);
      if (v) headers[name] = v;
    }
  }
  const res = await fetch(url, { ...init, headers, cache: 'no-store' });
  if (res.status === 401 || res.status === 404) return null;
  if (!res.ok) throw new Error(`API ${res.status}: ${await res.text()}`);
  return (await res.json()) as T;
}

export async function whoami(): Promise<Me | null> {
  return fetchJson<Me>('/api/v1/whoami');
}

export type ApiToken = {
  id: string;
  name: string;
  prefix: string;
  scopes: string[];
  kind: 'Service' | 'Admin';
  lastUsedAt: string | null;
  revokedAt: string | null;
  createdAt: string;
};

export async function listTokens(): Promise<ApiToken[] | null> {
  return fetchJson<ApiToken[]>('/api/admin/tokens/');
}

export async function listProjects(): Promise<Project[] | null> {
  return fetchJson<Project[]>('/api/v1/projects');
}

export async function getProject(slug: string): Promise<Project | null> {
  return fetchJson<Project>(`/api/v1/projects/${slug}`);
}

export async function listIssues(slug: string): Promise<Paged<Issue> | null> {
  return fetchJson<Paged<Issue>>(`/api/v1/projects/${slug}/issues?limit=50`);
}

export async function listStates(slug: string): Promise<State[] | null> {
  return fetchJson<State[]>(`/api/v1/projects/${slug}/states`);
}

export async function getIssue(slug: string, seq: number): Promise<Issue | null> {
  // The single-issue GET includes acceptanceCriteria inline (WAY-7).
  return fetchJson<Issue>(`/api/v1/projects/${slug}/issues/${seq}`);
}
