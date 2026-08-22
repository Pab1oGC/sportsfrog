declare global {
  interface ImportMeta {
    readonly env: Record<string, string | undefined>;
  }
}

export const DEFAULT_API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:8080';

export type ApiStatus = 'checking' | 'online' | 'offline';

export interface SessionOrganization {
  id: string;
  name: string;
  slug: string;
  role: string;
}

export interface SessionResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  organizations: SessionOrganization[];
}

export interface ApiClub {
  id: string;
  name: string;
  shortName: string | null;
  logoUrl: string | null;
  isActive: boolean;
}

export interface ApiAthlete {
  id: string;
  firstName: string;
  lastName: string;
  documentId: string;
  birthDate: string;
  gender: string | null;
  photoUrl: string | null;
  isActive: boolean;
}

export function buildApiUrl(path: string) {
  const base = DEFAULT_API_BASE.replace(/\/$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${base}${normalizedPath}`;
}

export async function apiCheckHealth() {
  const response = await fetch(buildApiUrl('/health'), {
    method: 'GET',
    headers: { Accept: 'application/json' },
  });

  return response.ok;
}

export async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem('sportfrog_access_token');
  const organizationId = localStorage.getItem('sportfrog_organization_id');

  const headers = new Headers(options.headers ?? {});
  headers.set('Accept', 'application/json');

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  if (organizationId) {
    headers.set('X-Organization-Id', organizationId);
  }

  if (!(options.body instanceof FormData) && !headers.has('Content-Type') && options.body !== undefined) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(buildApiUrl(path), {
    ...options,
    headers,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed with status ${response.status}`);
  }

  if (response.status === 204) {
    return null as T;
  }

  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('application/json')) {
    return (await response.json()) as T;
  }

  return (await response.text()) as unknown as T;
}

export function getApiClubs() {
  return apiFetch<ApiClub[]>('/clubs');
}

export function getApiAthletes() {
  return apiFetch<ApiAthlete[]>('/athletes');
}

export function createApiClub(name: string, shortName: string) {
  return apiFetch<{ id: string }>('/clubs', {
    method: 'POST',
    body: JSON.stringify({ name, shortName: shortName || null, logoUrl: null }),
  });
}

export function createApiAthlete(input: {
  firstName: string;
  lastName: string;
  documentId: string;
  birthDate: string;
  gender: string;
  guardianName?: string;
  guardianPhone?: string;
  photoUrl?: string;
}) {
  return apiFetch<{ id: string; alreadyRegistered: boolean }>('/athletes', {
    method: 'POST',
    body: JSON.stringify({
      ...input,
      guardianName: input.guardianName || null,
      guardianPhone: input.guardianPhone || null,
      photoUrl: input.photoUrl || null,
    }),
  });
}

export async function loginToApi(email: string, password: string) {
  const session = await apiFetch<SessionResponse>('/auth/session', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  });

  localStorage.setItem('sportfrog_access_token', session.accessToken);
  localStorage.setItem('sportfrog_refresh_token', session.refreshToken);

  if (session.organizations.length > 0) {
    localStorage.setItem('sportfrog_organization_id', session.organizations[0].id);
  }

  return session;
}

export function clearApiSession() {
  localStorage.removeItem('sportfrog_access_token');
  localStorage.removeItem('sportfrog_refresh_token');
  localStorage.removeItem('sportfrog_organization_id');
}
