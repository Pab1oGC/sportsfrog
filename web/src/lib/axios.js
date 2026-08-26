import axios from 'axios';

const axiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '',
  headers: { 'Content-Type': 'application/json' },
});

// Add auth token and org header to every request
axiosInstance.interceptors.request.use((config) => {
  const token = sessionStorage.getItem('jwt_access_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;

  try {
    const orgs = JSON.parse(localStorage.getItem('organizations') || '[]');
    if (orgs?.[0]?.id) config.headers['X-Organization-Id'] = orgs[0].id;
  } catch { /* ignore */ }

  return config;
});

// Normalize error messages
axiosInstance.interceptors.response.use(
  (r) => r,
  async (error) => {
    const response = error?.response;
    let body = response?.data;

    // A download is requested with responseType 'blob', and when it fails the
    // problem+json comes back as a Blob too: reading .detail off it yields
    // undefined and the real reason stays inside a file nobody opens.
    if (typeof Blob !== 'undefined' && body instanceof Blob) {
      try { body = JSON.parse(await body.text()); } catch { body = null; }
    }

    // A validation 400 carries no "detail": it carries "errors" with one
    // message per field and a generic "title" that names nothing. The
    // concrete reason comes first.
    const msg = body?.detail || fieldErrors(body) || body?.title || error?.message || 'Error';

    // Status and response are preserved because rejecting with a bare Error
    // forces every screen to guess a 404 from a 409 by reading text.
    const failure = new Error(msg);
    failure.status = response?.status;
    failure.response = response;
    failure.errors = body?.errors;
    return Promise.reject(failure);
  },
);

// Every message, not just the first: a form can have two bad fields, and
// showing one leaves the second waiting for another round trip.
function fieldErrors(body) {
  if (!body?.errors) return null;
  const messages = Object.values(body.errors).flatMap((v) => (Array.isArray(v) ? v : [String(v)]));
  return messages.length ? messages.join(' ') : null;
}

export default axiosInstance;

// ─── API Endpoints ────────────────────────────────────────────────────────────
const api = '/api';

export const endpoints = {
  // Auth
  auth: {
    signIn: `${api}/auth/session`,
    renew: `${api}/auth/session/renewal`,
    signOut: `${api}/auth/session/revocation`,
    signUp: `${api}/organizations`,
  },

  // Organizations
  organizations: { list: `${api}/organizations`, members: `${api}/members` },

  // Core entities
  sports: `${api}/sports`,
  sport: (code) => `${api}/sports/${code}`,
  rulesets: `${api}/rulesets`,
  ruleset: (id) => `${api}/rulesets/${id}`,
  clubs: `${api}/clubs`,
  club: (id) => `${api}/clubs/${id}`,
  athletes: `${api}/athletes`,
  athlete: (id) => `${api}/athletes/${id}`,
  athletePhotos: `${api}/athletes/photos/imports`,

  // Competitions
  competitions: `${api}/competitions`,
  competition: (id) => `${api}/competitions/${id}`,
  competitionStatus: (id) => `${api}/competitions/${id}/status`,
  competitionPublication: (id) => `${api}/competitions/${id}/publication`,
  competitionSchedule: (id) => `${api}/competitions/${id}/schedule`,
  competitionMatches: (id) => `${api}/competitions/${id}/matches`,

  // Categories
  categories: (compId) => `${api}/competitions/${compId}/categories`,
  category: (compId, catId) => `${api}/competitions/${compId}/categories/${catId}`,
  categoryMatches: (catId) => `${api}/categories/${catId}/matches`,
  categoryDraw: (catId) => `${api}/categories/${catId}/draw`,
  categoryAdvanceBracket: (catId) => `${api}/categories/${catId}/draw/next-round`,

  // Teams & Roster
  teams: (catId) => `${api}/categories/${catId}/teams`,
  team: (id) => `${api}/teams/${id}`,
  roster: (teamId) => `${api}/teams/${teamId}/roster`,
  rosterEntry: (id) => `${api}/roster/${id}`,
  rosterWithdrawal: (id) => `${api}/roster/${id}/withdrawal`,
  rosterTemplate: (teamId) => `${api}/teams/${teamId}/roster/import/template`,
  rosterImportPreview: (teamId) => `${api}/teams/${teamId}/roster/import/preview`,
  rosterImportApply: (teamId) => `${api}/teams/${teamId}/roster/import`,

  // Venues & Spaces
  venues: `${api}/venues`,
  venue: (id) => `${api}/venues/${id}`,
  venueSpaces: (venueId) => `${api}/venues/${venueId}/spaces`,
  spaces: `${api}/spaces`,
  space: (id) => `${api}/spaces/${id}`,

  // Matches & Events
  match: (id) => `${api}/matches/${id}`,
  matchResult: (id) => `${api}/matches/${id}/result`,
  matchStatus: (id) => `${api}/matches/${id}/status`,
  matchWalkover: (id) => `${api}/matches/${id}/walkover`,
  matchEvents: (matchId) => `${api}/matches/${matchId}/events`,
  event: (id) => `${api}/events/${id}`,

  // Standings & Leaders
  standings: (catId) => `${api}/categories/${catId}/standings`,
  leaders: (catId) => `${api}/categories/${catId}/leaders`,

  // Documents
  templates: `${api}/documents/templates`,
  template: (id) => `${api}/documents/templates/${id}`,
  documentsDesign: `${api}/documents/design`,
  templateVersion: (id, version) => `${api}/documents/templates/${id}/versions/${version}`,
  templateBackground: `${api}/documents/templates/backgrounds`,
  documentBatches: `${api}/documents/batches`,
  documentBatch: (id) => `${api}/documents/batches/${id}`,
  issuedDocuments: `${api}/documents/issued`,
  revokeDocument: (id) => `${api}/documents/issued/${id}/revoke`,

  // Public
  publicCompetitions: `${api}/public/competitions`,
  publicCompetition: (orgSlug, compSlug) => `${api}/public/${orgSlug}/${compSlug}`,
  publicMatches: (orgSlug, compSlug) => `${api}/public/${orgSlug}/${compSlug}/matches`,
  publicStandings: (orgSlug, compSlug) => `${api}/public/${orgSlug}/${compSlug}/standings`,
  publicLeaders: (orgSlug, compSlug) => `${api}/public/${orgSlug}/${compSlug}/leaders`,
  publicRoster: (orgSlug, compSlug, teamId) => `${api}/public/${orgSlug}/${compSlug}/teams/${teamId}/roster`,
  publicVerify: (orgSlug, serial) => `${api}/public/verify/${orgSlug}/${serial}`,
};