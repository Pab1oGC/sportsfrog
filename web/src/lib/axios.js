import axios from 'axios';
import { JWT_STORAGE_KEY } from 'src/auth/context/jwt/constant';
import { leerRenovacion, guardarRenovacion, seRecuerda, olvidarRenovacion } from 'src/auth/context/jwt/session-store';

const axiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '',
  headers: { 'Content-Type': 'application/json' },
});

// Add auth token and org header to every request
axiosInstance.interceptors.request.use((config) => {
  const token = sessionStorage.getItem(JWT_STORAGE_KEY);
  if (token) config.headers.Authorization = `Bearer ${token}`;

  try {
    const orgs = JSON.parse(localStorage.getItem('organizations') || '[]');
    if (orgs?.[0]?.id) config.headers['X-Organization-Id'] = orgs[0].id;
  } catch { /* ignore */ }

  // Cuando se envía FormData, dejar que el navegador/axios calcule el Content-Type multipart con su boundary
  if (typeof FormData !== 'undefined' && config.data instanceof FormData) {
    delete config.headers['Content-Type'];
  }

  return config;
});

// ─── Renovación automática ──────────────────────────────────────────────────
//
// El token de acceso dura poco a propósito; lo que faltaba era que su
// vencimiento no se notara. Antes, un 401 llegaba tal cual a la pantalla que
// lo pidió, sin que nadie intentara canjear el refresh token primero.
//
// Una sola renovación en vuelo: el refresh token rota en cada uso (la API
// anula el anterior al emitir uno nuevo), así que si dos pedidos vencen a la
// vez y cada uno intentara renovar por su cuenta, el segundo usaría un token
// que el primero ya dejó sin validez. Todos esperan la misma promesa.
let renovacionEnCurso = null;

function renovarSesion() {
  if (!renovacionEnCurso) {
    renovacionEnCurso = renovarSesionReal().finally(() => { renovacionEnCurso = null; });
  }
  return renovacionEnCurso;
}

async function renovarSesionReal() {
  const refreshToken = leerRenovacion();
  if (!refreshToken) return null;

  try {
    const { data } = await axiosInstance.post(endpoints.auth.renew, { refreshToken });
    sessionStorage.setItem(JWT_STORAGE_KEY, data.accessToken);
    guardarRenovacion(data.refreshToken, seRecuerda());
    // Mismo efecto que action.js:establecer() sobre esta clave: una
    // renovación silenciosa no debe dejar una lista de organizaciones vieja
    // sirviendo si algo cambió del lado del servidor.
    try { localStorage.setItem('organizations', JSON.stringify(data.organizations || [])); } catch { /* ignore */ }
    return data.accessToken;
  } catch {
    sessionStorage.removeItem(JWT_STORAGE_KEY);
    olvidarRenovacion();
    return null;
  }
}

// Normalize error messages
axiosInstance.interceptors.response.use(
  (r) => r,
  async (error) => {
    const response = error?.response;
    const config = error?.config;

    // Un 401 en cualquier pedido que no sea el de renovación (o el de login,
    // que ya contesta 401 por sí mismo cuando la contraseña es incorrecta)
    // intenta canjear el refresh token una vez y reintenta el pedido original
    // en vez de dejarlo fallar. Marcado en el propio config para que el
    // reintento, si también da 401, no vuelva a intentar renovar.
    if (
      response?.status === 401 && config && !config._retriedAfterRenewal
      && config.url !== endpoints.auth.renew && config.url !== endpoints.auth.signIn
    ) {
      config._retriedAfterRenewal = true;
      const nuevoToken = await renovarSesion();

      if (nuevoToken) {
        return axiosInstance(config);
      }

      // Sin refresh token, o la API lo rechazó: no hay con qué seguir. Se
      // manda al login en vez de dejar la pantalla mostrando errores de una
      // sesión que ya terminó.
      window.location.href = '/auth/jwt/sign-in';
    }

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
  competitionBulletinPdf: (id) => `${api}/competitions/${id}/bulletin.pdf`,
  competitionBulletinWord: (id) => `${api}/competitions/${id}/bulletin.docx`,
  competitionMatches: (id) => `${api}/competitions/${id}/matches`,
  competitionFixturePdf: (id) => `${api}/competitions/${id}/fixture.pdf`,
  categoryFixturePdf: (catId) => `${api}/categories/${catId}/fixture.pdf`,

  // Categories
  categories: (compId) => `${api}/competitions/${compId}/categories`,
  category: (compId, catId) => `${api}/competitions/${compId}/categories/${catId}`,
  categoryMatches: (catId) => `${api}/categories/${catId}/matches`,
  categoryDraw: (catId) => `${api}/categories/${catId}/draw`,
  categoryDrawGroups: (catId) => `${api}/categories/${catId}/draw/groups`,
  categoryAdvanceBracket: (catId) => `${api}/categories/${catId}/draw/next-round`,
  categoryPromoteGroupStage: (catId) => `${api}/categories/${catId}/draw/knockout`,

  // Un deporte individual (sports.isIndividual) inscribe por deportista, no
  // por club -- ver EnrollIndividual en el backend. Las tres restantes son
  // la etapa de clasificacion de un deporte juzgado (sports.scoreMode ===
  // 'judged'): abrirla, leerla, y promover a los que clasifican a la
  // eliminatoria -- el equivalente de categoryDraw/categoryPromoteGroupStage
  // de arriba para un deporte que no se decide por tabla.
  categoryIndividuals: (catId) => `${api}/categories/${catId}/individuals`,
  categoryPerformances: (catId) => `${api}/categories/${catId}/performances`,
  categoryPerformancesOpen: (catId) => `${api}/categories/${catId}/performances/open`,
  categoryPromoteClassification: (catId) => `${api}/categories/${catId}/performances/promote`,

  // Teams & Roster
  teams: (catId) => `${api}/categories/${catId}/teams`,
  team: (id) => `${api}/teams/${id}`,
  roster: (teamId) => `${api}/teams/${teamId}/roster`,
  rosterRegisterBulk: (teamId) => `${api}/teams/${teamId}/roster/register-bulk`,
  rosterEntry: (id) => `${api}/roster/${id}`,
  rosterWithdrawal: (id) => `${api}/roster/${id}/withdrawal`,
  rosterTemplate: (teamId) => `${api}/teams/${teamId}/roster/import/template`,
  rosterImportPreview: (teamId) => `${api}/teams/${teamId}/roster/import/preview`,
  rosterImportApply: (teamId) => `${api}/teams/${teamId}/roster/import`,
  // Hermano del de arriba, pero para deportes individuales: no hay un
  // equipo al que apuntar el archivo (cada fila crea el suyo), asi que se
  // pide por competencia + club en vez de por equipo.
  delegationRosterTemplate: (competitionId, clubId) => `${api}/competitions/${competitionId}/clubs/${clubId}/individuals/import/template`,
  delegationRosterImportPreview: (competitionId, clubId) => `${api}/competitions/${competitionId}/clubs/${clubId}/individuals/import/preview`,
  delegationRosterImportApply: (competitionId, clubId) => `${api}/competitions/${competitionId}/clubs/${clubId}/individuals/import`,
  teamReport: (id) => `${api}/teams/${id}/report`,
  teamReportPdf: (id) => `${api}/teams/${id}/report.pdf`,
  athleteReport: (id) => `${api}/athletes/${id}/report`,
  athleteReportPdf: (id) => `${api}/athletes/${id}/report.pdf`,

  // Venues & Spaces
  venues: `${api}/venues`,
  venue: (id) => `${api}/venues/${id}`,
  resolveVenueMapsLink: (url) => `${api}/venues/resolve-maps-link?url=${encodeURIComponent(url)}`,
  venueSpaces: (venueId) => `${api}/venues/${venueId}/spaces`,
  spaces: `${api}/spaces`,
  space: (id) => `${api}/spaces/${id}`,

  // Matches & Events
  match: (id) => `${api}/matches/${id}`,
  matchResult: (id) => `${api}/matches/${id}/result`,
  matchResultFromEvents: (id) => `${api}/matches/${id}/result/from-events`,
  matchStatus: (id) => `${api}/matches/${id}/status`,
  matchWalkover: (id) => `${api}/matches/${id}/walkover`,
  matchPenalties: (id) => `${api}/matches/${id}/penalties`,
  matchEvents: (matchId) => `${api}/matches/${matchId}/events`,
  matchesRescheduleBulk: `${api}/matches/reschedule-bulk`,
  event: (id) => `${api}/events/${id}`,

  // Standings & Leaders
  standings: (catId) => `${api}/categories/${catId}/standings`,
  leaders: (catId) => `${api}/categories/${catId}/leaders`,

  // Cargar el puntaje de una actuacion puntual -- performanceId, no catId,
  // por eso vive aparte de las categoryPerformances* de arriba, igual que
  // matchResult vive aparte de categoryMatches.
  performanceScore: (id) => `${api}/performances/${id}/score`,
  performanceSchedule: (id) => `${api}/performances/${id}/schedule`,
  performancesRescheduleBulk: `${api}/performances/reschedule-bulk`,

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
  publicClassification: (orgSlug, compSlug) => `${api}/public/${orgSlug}/${compSlug}/classification`,
  publicRoster: (orgSlug, compSlug, teamId) => `${api}/public/${orgSlug}/${compSlug}/teams/${teamId}/roster`,
  publicVerify: (orgSlug, serial) => `${api}/public/verify/${orgSlug}/${serial}`,
};