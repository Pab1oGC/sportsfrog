import { Activity, CalendarDays, ChevronRight, Clock3, FileBadge2, Flag, LayoutDashboard, MapPinned, PlayCircle, Plus, ShieldCheck, UserRound, Users, Trophy, BarChart3, ArrowUpRight, TimerReset, CirclePlay, Pause, Download, Pencil, Trash2, Bell, QrCode, X, Eye } from 'lucide-react';
import { Route, Routes, NavLink } from 'react-router-dom';
import { useEffect, useMemo, useState } from 'react';
import { matchEvents as initialMatchEvents, matches as initialMatches, players as initialPlayers, teams as initialTeams, tournaments as initialTournaments, venues as initialVenues } from './data/mockData';
import { useAppContext } from './context/AppContext';
import { useSportfrogApi } from './hooks/useSportfrogApi';
import { createApiAthlete, createApiClub } from './services/api';
import { AthleteModal, type AthleteFormValues } from './components/AthleteModal';
import type { Match, MatchEvent, TournamentStatus } from './types';

const navItems = [
  { label: 'Dashboard', icon: LayoutDashboard, to: '/' },
  { label: 'Campeonatos', icon: Trophy, to: '/tournaments' },
  { label: 'Equipos', icon: Users, to: '/teams' },
  { label: 'Sedes', icon: MapPinned, to: '/venues' },
  { label: 'Fixture', icon: CalendarDays, to: '/schedule' },
  { label: 'En vivo', icon: PlayCircle, to: '/live' },
  { label: 'Indicadores', icon: BarChart3, to: '/stats' },
  { label: 'Credenciales', icon: FileBadge2, to: '/credentials' },
  { label: 'Portal', icon: GlobeIcon, to: '/portal' },
] as const;

const statusStyles: Record<TournamentStatus, string> = {
  'En curso': 'bg-emerald-100 text-emerald-700',
  Finalizado: 'bg-slate-200 text-slate-700',
  Próximo: 'bg-cyan-100 text-cyan-700',
};

function triggerToast(message: string) {
  window.dispatchEvent(new CustomEvent('sf:toast', { detail: { message } }));
}

function openQuickModal(title: string, kind: 'generic' | 'schedule' | 'event' = 'generic') {
  window.dispatchEvent(new CustomEvent('sf:quick-modal', { detail: { title, kind } }));
}

function GlobeIcon(props: Record<string, unknown>) {
  return <span {...props} className="inline-flex h-5 w-5 items-center justify-center text-[1rem]">🌐</span>;
}

function AdminLayout({ children }: { children: React.ReactNode }) {
  const [showModal, setShowModal] = useState(false);
  const [showApiLogin, setShowApiLogin] = useState(false);
  const [apiEmail, setApiEmail] = useState('admin@rc.com');
  const [apiPassword, setApiPassword] = useState('Password123!');
  const [apiError, setApiError] = useState<string | null>(null);
  const [modalTitle, setModalTitle] = useState('Crear nuevo elemento');
  const [modalKind, setModalKind] = useState<'generic' | 'schedule' | 'event'>('generic');
  const [toast, setToast] = useState<string | null>(null);
  const { activeSport, setActiveSport, sportConfigs } = useAppContext();
  const { status, isAuthenticated, login, logout } = useSportfrogApi();

  useEffect(() => {
    const handleQuickModal = (event: Event) => {
      const detail = (event as CustomEvent<{ title?: string; kind?: 'generic' | 'schedule' | 'event' }>).detail;
      setModalTitle(detail?.title ?? 'Crear nuevo elemento');
      setModalKind(detail?.kind ?? 'generic');
      setShowModal(true);
    };

    const handleToast = (event: Event) => {
      const detail = (event as CustomEvent<{ message?: string }>).detail;
      if (detail?.message) {
        setToast(detail.message);
      }
    };

    window.addEventListener('sf:quick-modal', handleQuickModal);
    window.addEventListener('sf:toast', handleToast);

    return () => {
      window.removeEventListener('sf:quick-modal', handleQuickModal);
      window.removeEventListener('sf:toast', handleToast);
    };
  }, []);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 1800);
    return () => window.clearTimeout(timer);
  }, [toast]);

  return (
    <div className="min-h-screen bg-bgLight text-textPrimary">
      <div className="flex min-h-screen">
        <aside className="w-72 border-r border-slate-200 bg-primary text-white shadow-soft">
          <div className="flex items-center gap-3 px-6 py-7 border-b border-white/10">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/10 text-accent shadow-lg shadow-accent/20">
              <Trophy className="h-5 w-5" />
            </div>
            <div>
              <div className="font-display text-xl font-bold">SportFrog</div>
              <div className="text-xs text-sky-100">Control Deportivo</div>
            </div>
          </div>

          <nav className="space-y-2 px-4 py-6">
            {navItems.map(({ label, icon: Icon, to }) => (
              <NavLink
                key={label}
                to={to}
                className={({ isActive }) =>
                  `group flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-all duration-200 ease-in-out ${
                    isActive ? 'bg-white/10 text-white shadow-inner' : 'text-slate-200 hover:bg-white/5 hover:text-white'
                  }`
                }
              >
                <Icon className="h-4 w-4" />
                {label}
              </NavLink>
            ))}
          </nav>

          <div className="mx-4 mt-4 rounded-2xl border border-white/10 bg-white/5 p-4 backdrop-blur-sm">
            <div className="flex items-center justify-between text-xs uppercase tracking-[0.2em] text-sky-100">
              <span>Panel</span>
              <ShieldCheck className="h-4 w-4" />
            </div>
            <div className="mt-3 text-2xl font-bold text-white">12.4k</div>
            <div className="mt-1 text-xs text-slate-200">actividades registradas este mes</div>
          </div>
        </aside>

        <div className="flex-1">
          <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/80 backdrop-blur-md">
            <div className="flex h-20 items-center justify-between px-6">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-textMuted">Administración</p>
                <h1 className="mt-1 text-2xl font-bold text-textPrimary">Panel de gestión deportiva</h1>
              </div>

              <div className="flex items-center gap-3">
                <div className="inline-flex items-center gap-2 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-emerald-700">
                  <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
                  Demo live
                </div>
                <div className="rounded-xl border border-slate-200 bg-slate-50 px-2 py-1.5 shadow-sm">
                  <select
                    value={activeSport}
                    onChange={(event) => setActiveSport(event.target.value as keyof typeof sportConfigs)}
                    className="bg-transparent text-sm font-medium text-slate-700 outline-none"
                  >
                    {Object.values(sportConfigs).map((sport) => (
                      <option key={sport.code} value={sport.code}>{sport.name}</option>
                    ))}
                  </select>
                </div>
                <button
                  type="button"
                  onClick={() => triggerToast('Notificaciones actualizadas')}
                  className="relative rounded-full border border-slate-200 bg-white p-2.5 text-textMuted transition-all duration-200 ease-in-out hover:border-primary hover:text-primary hover:shadow-sm"
                >
                  <Bell className="h-4 w-4" />
                  <span className="absolute right-1.5 top-1.5 h-2.5 w-2.5 rounded-full bg-accent animate-pulse" />
                </button>
                <button
                  type="button"
                  onClick={() => openQuickModal('Crear nuevo elemento')}
                  className="flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white shadow-lg shadow-primary/20 transition-all duration-200 ease-in-out hover:-translate-y-0.5 hover:bg-primary/90"
                >
                  <Plus className="h-4 w-4" />
                  Nuevo
                </button>
                <button
                  type="button"
                  onClick={() => {
                    if (isAuthenticated) {
                      logout();
                      triggerToast('Sesión de API cerrada');
                      return;
                    }

                    setApiError(null);
                    setShowApiLogin(true);
                  }}
                  className={
                    isAuthenticated
                      ? 'rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm font-medium text-emerald-700 transition hover:border-emerald-300'
                      : 'rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 transition hover:border-primary hover:text-primary'
                  }
                >
                  {isAuthenticated ? `API ${status === 'online' ? 'online' : 'activa'}` : 'Conectar API'}
                </button>
                <div className="flex items-center gap-3 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 shadow-sm">
                  <div className="flex h-9 w-9 items-center justify-center rounded-full bg-primary text-sm font-bold text-white">AR</div>
                  <div>
                    <div className="text-sm font-semibold">Alejandro Ruiz</div>
                    <div className="text-xs text-textMuted">Director deportivo</div>
                  </div>
                </div>
              </div>
            </div>
          </header>

          <main className="p-6">{children}</main>
        </div>
      </div>

      {showModal && (
        <div className="fixed inset-0 z-30 flex items-center justify-center bg-slate-950/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl">
            <div className="mb-5 flex items-center justify-between">
              <h3 className="text-xl font-bold">{modalTitle}</h3>
              <button type="button" onClick={() => setShowModal(false)} className="rounded-full p-2 hover:bg-slate-100">×</button>
            </div>

            {modalKind === 'schedule' ? (
              <div className="grid gap-4">
                <Field label="Equipo local" value="Club Atlético Norte" />
                <Field label="Equipo visitante" value="Sporting Sur" />
                <Field label="Fecha" value="2026-08-26" />
                <Field label="Hora" value="19:00" />
                <Field label="Cancha" value="Estadio Nacional" />
                <div className="flex justify-end gap-3 pt-4">
                  <button type="button" onClick={() => setShowModal(false)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:bg-slate-50">Cancelar</button>
                  <button
                    type="button"
                    onClick={() => {
                      setShowModal(false);
                      triggerToast('Partido programado correctamente');
                    }}
                    className="rounded-xl bg-primary px-4 py-2 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90"
                  >
                    Guardar partido
                  </button>
                </div>
              </div>
            ) : modalKind === 'event' ? (
              <div className="grid gap-4">
                <label className="block text-sm">
                  <span className="mb-1.5 block font-medium text-slate-600">Tipo</span>
                  <select defaultValue="Gol" className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none transition-all duration-200 ease-in-out focus:border-primary focus:bg-white">
                    <option>Gol</option>
                    <option>Tarjeta Amarilla</option>
                    <option>Tarjeta Roja</option>
                    <option>Walkover</option>
                  </select>
                </label>
                <Field label="Minuto" value="43" />
                <Field label="Jugador" value="Valentina Cruz" />
                <Field label="Equipo" value="Local" />
                <div className="flex justify-end gap-3 pt-4">
                  <button type="button" onClick={() => setShowModal(false)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:bg-slate-50">Cancelar</button>
                  <button
                    type="button"
                    onClick={() => {
                      setShowModal(false);
                      triggerToast('Evento registrado en la cronología');
                    }}
                    className="rounded-xl bg-accent px-4 py-2 text-sm font-medium text-slate-900 transition hover:-translate-y-0.5 hover:bg-cyan-400"
                  >
                    Guardar evento
                  </button>
                </div>
              </div>
            ) : (
              <div className="grid gap-4">
                <Field label="Nombre" value="Campeonato Regional" />
                <Field label="Tipo" value="Liga" />
                <Field label="Categoría" value="Sub-20" />
                <div className="flex justify-end gap-3 pt-4">
                  <button type="button" onClick={() => setShowModal(false)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:bg-slate-50">Cancelar</button>
                  <button
                    type="button"
                    onClick={() => {
                      setShowModal(false);
                      triggerToast('Datos guardados correctamente');
                    }}
                    className="rounded-xl bg-primary px-4 py-2 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90"
                  >
                    Guardar
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {toast && (
        <div className="fixed bottom-5 right-5 z-40 rounded-xl bg-slate-900 px-4 py-3 text-sm font-medium text-white shadow-2xl">
          {toast}
        </div>
      )}

      {showApiLogin && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-950/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl">
            <div className="mb-5 flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-textMuted">Autenticación</p>
                <h3 className="mt-1 text-xl font-bold">Conectar con SportFrog API</h3>
              </div>
              <button type="button" onClick={() => setShowApiLogin(false)} className="rounded-full p-2 hover:bg-slate-100">×</button>
            </div>

            <div className="space-y-4">
              <label className="block text-sm">
                <span className="mb-1.5 block font-medium text-slate-600">Email</span>
                <input
                  value={apiEmail}
                  onChange={(event) => setApiEmail(event.target.value)}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none transition focus:border-primary focus:bg-white"
                />
              </label>

              <label className="block text-sm">
                <span className="mb-1.5 block font-medium text-slate-600">Password</span>
                <input
                  type="password"
                  value={apiPassword}
                  onChange={(event) => setApiPassword(event.target.value)}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none transition focus:border-primary focus:bg-white"
                />
              </label>

              {apiError && <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{apiError}</div>}

              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={() => setShowApiLogin(false)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:bg-slate-50">Cancelar</button>
                <button
                  type="button"
                  onClick={async () => {
                    try {
                      await login(apiEmail, apiPassword);
                      window.dispatchEvent(new Event('sf:api-session-changed'));
                      setShowApiLogin(false);
                      triggerToast('API conectada correctamente');
                    } catch (error) {
                      setApiError(error instanceof Error ? error.message : 'No se pudo iniciar sesión');
                    }
                  }}
                  className="rounded-xl bg-primary px-4 py-2 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90"
                >
                  Iniciar sesión
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <label className="block text-sm">
      <span className="mb-1.5 block font-medium text-slate-600">{label}</span>
      <input defaultValue={value} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none transition-all duration-200 ease-in-out focus:border-primary focus:bg-white" />
    </label>
  );
}

function DashboardPage() {
  const { activeSport, sportConfigs, categories, matches, tournaments } = useAppContext();
  const config = sportConfigs[activeSport];
  const [selectedCategory, setSelectedCategory] = useState('Todas las categorías');
  const [selectedMatch, setSelectedMatch] = useState<Match | null>(null);

  useEffect(() => {
    setSelectedCategory('Todas las categorías');
  }, [activeSport]);

  const categoryOptions = useMemo(
    () => ['Todas las categorías', ...categories.filter((category) => category.sport === activeSport).map((category) => category.name)],
    [categories, activeSport],
  );

  const activeCategoryMeta = useMemo(() => {
    if (selectedCategory === 'Todas las categorías') {
      return {
        name: selectedCategory,
        periodDurationMinutes: config.defaultPeriodMinutes,
        defaultPeriods: config.defaultPeriods,
      };
    }

    return categories.find((category) => category.sport === activeSport && category.name === selectedCategory)
      ?? {
        name: selectedCategory,
        periodDurationMinutes: config.defaultPeriodMinutes,
        defaultPeriods: config.defaultPeriods,
      };
  }, [categories, activeSport, config.defaultPeriodMinutes, config.defaultPeriods, selectedCategory]);

  const matchMapBySport: Record<string, number[]> = {
    FOOTBALL: [1, 3],
    FUTSAL: [2, 4],
    BASKETBALL: [1, 4],
    VOLLEYBALL: [2, 3],
    WALLY: [3, 4],
  };

  const categoryMatchMap: Record<string, Record<string, number[]>> = {
    FOOTBALL: {
      'Todas las categorías': [1, 3],
      'Sub-15': [1],
      'Mayores': [3],
    },
    FUTSAL: {
      'Todas las categorías': [2, 4],
      'Futsal Elite': [2, 4],
    },
    BASKETBALL: {
      'Todas las categorías': [1, 4],
      'Sub-15': [1],
      'Mayores': [4],
    },
    VOLLEYBALL: {
      'Todas las categorías': [2, 3],
      'Mayores': [2, 3],
    },
    WALLY: {
      'Todas las categorías': [3, 4],
      'Mayores': [3, 4],
    },
  };

  const filteredMatches = matches.filter((match) => {
    const allowedIds = categoryMatchMap[activeSport]?.[selectedCategory] ?? matchMapBySport[activeSport] ?? [];
    return allowedIds.includes(match.id);
  });

  const kpiMap: Record<string, Record<string, { tournaments: string; teams: string; players: string; meetings: string }>> = {
    FOOTBALL: {
      'Todas las categorías': { tournaments: '12', teams: '28', players: '1.240', meetings: '8' },
      'Sub-15': { tournaments: '4', teams: '10', players: '420', meetings: '3' },
      'Mayores': { tournaments: '8', teams: '18', players: '820', meetings: '5' },
    },
    FUTSAL: {
      'Todas las categorías': { tournaments: '9', teams: '16', players: '640', meetings: '6' },
      'Futsal Elite': { tournaments: '5', teams: '8', players: '320', meetings: '4' },
    },
    BASKETBALL: {
      'Todas las categorías': { tournaments: '11', teams: '20', players: '780', meetings: '7' },
      'Sub-15': { tournaments: '4', teams: '8', players: '250', meetings: '2' },
      'Mayores': { tournaments: '7', teams: '12', players: '530', meetings: '5' },
    },
    VOLLEYBALL: {
      'Todas las categorías': { tournaments: '7', teams: '14', players: '610', meetings: '5' },
      'Mayores': { tournaments: '5', teams: '10', players: '420', meetings: '3' },
    },
    WALLY: {
      'Todas las categorías': { tournaments: '5', teams: '9', players: '280', meetings: '4' },
      'Mayores': { tournaments: '3', teams: '6', players: '180', meetings: '2' },
    },
  };

  const currentKpis = kpiMap[activeSport]?.[selectedCategory] ?? kpiMap[activeSport]?.['Todas las categorías'];

  const kpis = [
    { label: 'Torneos activos', value: currentKpis.tournaments, icon: Trophy, accent: 'bg-primary/10 text-primary', trend: '+12%' },
    { label: 'Equipos activos', value: currentKpis.teams, icon: Users, accent: 'bg-secondary/10 text-secondary', trend: '+8%' },
    { label: 'Jugadores inscritos', value: currentKpis.players, icon: UserRound, accent: 'bg-cyan-100 text-accent', trend: '+17%' },
    { label: 'Partidos programados', value: currentKpis.meetings, icon: CalendarDays, accent: 'bg-emerald-100 text-emerald-600', trend: '4 en vivo' },
  ];

  const timelineByMatch: Record<number, MatchEvent[]> = {
    1: initialMatchEvents,
    2: [initialMatchEvents[1], initialMatchEvents[2]],
    3: [initialMatchEvents[0], initialMatchEvents[3]],
    4: [initialMatchEvents[4]],
  };

  const matchStats = selectedMatch ? timelineByMatch[selectedMatch.id] ?? [] : [];

  return (
    <div className="space-y-6">
      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-soft">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
          <div>
            <div className="mb-2 flex items-center gap-2">
              <span className="inline-flex items-center gap-2 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-emerald-700">
                <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
                Operación en vivo
              </span>
            </div>
            <p className="text-xs uppercase tracking-[0.2em] text-textMuted">Multideporte</p>
            <h2 className="text-2xl font-bold text-textPrimary">Dashboard {config.name}</h2>
          </div>

          <div className="flex flex-col gap-3 sm:flex-row">
            <select
              value={activeSport}
              onChange={(event) => { setSelectedCategory('Todas las categorías'); window.dispatchEvent(new CustomEvent('sf:quick-modal', { detail: { title: `${sportConfigs[event.target.value as keyof typeof sportConfigs].name} activado` } })); }}
              className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-medium text-slate-700 outline-none focus:border-primary"
            >
              {Object.values(sportConfigs).map((sport) => (
                <option key={sport.code} value={sport.code}>{sport.name}</option>
              ))}
            </select>

            <select
              value={selectedCategory}
              onChange={(event) => setSelectedCategory(event.target.value)}
              className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-medium text-slate-700 outline-none focus:border-primary"
            >
              {categoryOptions.map((category) => (
                <option key={category} value={category}>{category}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {kpis.map((item) => (
          <div key={item.label} className="card-surface p-5">
            <div className="flex items-center justify-between">
              <div className={`rounded-xl p-3 ${item.accent}`}><item.icon className="h-5 w-5" /></div>
              <span className="text-xs font-semibold text-emerald-600">{item.trend}</span>
            </div>
            <div className="mt-6 text-3xl font-bold text-textPrimary">{item.value}</div>
            <div className="mt-1 text-sm text-textMuted">{item.label}</div>
          </div>
        ))}
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.35fr_0.65fr]">
        <div className="card-surface p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h2 className="text-xl font-bold">Partidos recientes y próximos</h2>
              <p className="text-sm text-textMuted">{config.name} • {selectedCategory}</p>
            </div>
            <button
              type="button"
              onClick={() => triggerToast('Agenda actualizada con los próximos partidos')}
              className="inline-flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-600 transition-all duration-200 ease-in-out hover:border-primary hover:text-primary hover:-translate-y-0.5"
            >
              Ver todos <ChevronRight className="h-4 w-4" />
            </button>
          </div>

          <div className="space-y-3">
            {filteredMatches.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-6 text-center text-sm text-textMuted">
                No hay partidos disponibles para la categoría seleccionada.
              </div>
            ) : (
              filteredMatches.map((match) => (
                <button
                  type="button"
                  key={match.id}
                  onClick={() => setSelectedMatch(match)}
                  className="w-full rounded-2xl border border-slate-200 bg-slate-50 p-4 text-left transition-all duration-200 ease-in-out hover:-translate-y-0.5 hover:border-primary hover:bg-white"
                >
                  <div className="mb-3 flex items-center justify-between text-xs text-textMuted">
                    <span>{match.phase}</span>
                    <span className={`badge-pill ${match.status === 'En vivo' ? 'bg-emerald-100 text-emerald-700' : match.status === 'Finalizado' ? 'bg-slate-200 text-slate-700' : 'bg-cyan-100 text-cyan-700'}`}>{match.status}</span>
                  </div>
                  <div className="flex items-center justify-between gap-4">
                    <div className="flex-1 text-center">
                      <div className="text-lg font-bold">{match.homeTeam}</div>
                      <div className="text-3xl font-black text-primary">{match.homeScore}</div>
                    </div>
                    <div className="text-sm font-medium text-textMuted">vs</div>
                    <div className="flex-1 text-center">
                      <div className="text-lg font-bold">{match.awayTeam}</div>
                      <div className="text-3xl font-black text-secondary">{match.awayScore}</div>
                    </div>
                  </div>
                  <div className="mt-3 flex items-center justify-between text-xs text-textMuted">
                    <span><CalendarDays className="mr-1 inline h-3.5 w-3.5" />{match.date}</span>
                    <span><Clock3 className="mr-1 inline h-3.5 w-3.5" />{match.time}</span>
                  </div>
                </button>
              ))
            )}
          </div>
        </div>

        <div className="space-y-6">
          <div className="card-surface p-5">
            <h3 className="text-lg font-bold">Accesos directos</h3>
            <div className="mt-4 space-y-3">
              {[
                { label: 'Crear torneo', icon: Plus, className: 'bg-primary text-white', action: 'Crear torneo' },
                { label: 'Programar partido', icon: CalendarDays, className: 'bg-secondary text-white', action: 'Programar partido', kind: 'schedule' as const },
                { label: 'Registrar eventos', icon: Flag, className: 'bg-accent text-slate-900', action: 'Registrar evento del partido', kind: 'event' as const },
              ].map(({ label, icon: Icon, className, action, kind }) => (
                <button
                  type="button"
                  key={label}
                  onClick={() => {
                    openQuickModal(action, kind ?? 'generic');
                    triggerToast(`${action} abierto`);
                  }}
                  className={`flex w-full items-center justify-between rounded-2xl p-3.5 text-left text-sm font-semibold transition-all duration-200 ease-in-out hover:-translate-y-0.5 hover:shadow-lg ${className}`}
                >
                  <span className="inline-flex items-center gap-2"><Icon className="h-4 w-4" />{label}</span>
                  <ArrowUpRight className="h-4 w-4" />
                </button>
              ))}
            </div>
          </div>

          <div className="card-surface p-5">
            <div className="mb-4 flex items-center justify-between">
              <h3 className="text-lg font-bold">Reglas del deporte activo</h3>
              <span className="badge-pill bg-cyan-100 text-cyan-700">{config.name}</span>
            </div>
            <div className="space-y-3">
              <div className="rounded-2xl bg-slate-50 p-3">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-textMuted">Modo de puntaje</span>
                  <span className="font-semibold text-textPrimary">{config.scoreMode}</span>
                </div>
              </div>
              <div className="rounded-2xl bg-slate-50 p-3">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-textMuted">Períodos</span>
                  <span className="font-semibold text-textPrimary">{config.defaultPeriods}</span>
                </div>
              </div>
              <div className="rounded-2xl bg-slate-50 p-3">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-textMuted">Duración por categoría</span>
                  <span className="font-semibold text-textPrimary">{activeCategoryMeta.periodDurationMinutes ?? config.defaultPeriodMinutes} min</span>
                </div>
              </div>
              <div className="rounded-2xl border border-primary/10 bg-primary/5 p-3">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-textMuted">Categoría activa</span>
                  <span className="font-semibold text-primary">{selectedCategory}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {selectedMatch && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4 backdrop-blur-sm">
          <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl animate-[fadeInUp_0.2s_ease-out]">
            <div className="flex items-start justify-between gap-4 border-b border-slate-200 pb-4">
              <div className="flex flex-wrap items-center gap-2">
                <span className="badge-pill bg-primary/10 text-primary">{config.name}</span>
                <span className="badge-pill bg-cyan-100 text-cyan-700">{selectedCategory}</span>
                <span className={`badge-pill ${selectedMatch.status === 'En vivo' ? 'bg-emerald-100 text-emerald-700' : selectedMatch.status === 'Finalizado' ? 'bg-slate-200 text-slate-700' : 'bg-cyan-100 text-cyan-700'}`}>
                  {selectedMatch.status}
                </span>
              </div>
              <button type="button" onClick={() => setSelectedMatch(null)} className="rounded-full border border-slate-200 p-2 text-slate-500 transition hover:border-slate-300 hover:text-slate-700">
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="mt-6 rounded-2xl bg-slate-50 p-5">
              <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div className="flex items-center gap-4">
                  <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-primary/10 text-lg font-black text-primary">CAN</div>
                  <div>
                    <div className="text-lg font-bold text-textPrimary">{selectedMatch.homeTeam}</div>
                    <div className="text-xs uppercase tracking-[0.2em] text-textMuted">Local</div>
                  </div>
                </div>

                <div className="flex items-center gap-3 font-mono text-4xl font-black text-[#006098]">
                  <span>{selectedMatch.homeScore}</span>
                  <span className="text-slate-400">:</span>
                  <span>{selectedMatch.awayScore}</span>
                </div>

                <div className="flex items-center gap-4 md:flex-row-reverse">
                  <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-secondary/10 text-lg font-black text-secondary">SS</div>
                  <div className="text-right md:text-left">
                    <div className="text-lg font-bold text-textPrimary">{selectedMatch.awayTeam}</div>
                    <div className="text-xs uppercase tracking-[0.2em] text-textMuted">Visitante</div>
                  </div>
                </div>
              </div>
            </div>

            <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
              {[
                { label: 'Cancha', value: selectedMatch.court, icon: MapPinned },
                { label: 'Fecha', value: selectedMatch.date, icon: CalendarDays },
                { label: 'Horario', value: selectedMatch.time, icon: Clock3 },
                { label: 'Árbitros', value: `${selectedMatch.ref} / ${selectedMatch.assistants[0]}`, icon: Activity },
              ].map(({ label, value, icon: Icon }) => (
                <div key={label} className="rounded-2xl border border-slate-200 bg-slate-50 p-3">
                  <div className="flex items-center gap-2 text-xs uppercase tracking-[0.15em] text-textMuted">
                    <Icon className="h-4 w-4 text-primary" /> {label}
                  </div>
                  <div className="mt-2 text-sm font-semibold text-textPrimary">{value}</div>
                </div>
              ))}
            </div>

            <div className="mt-6">
              <div className="mb-3 flex items-center justify-between">
                <h3 className="text-lg font-bold text-textPrimary">Estadísticas e incidencias</h3>
                <button type="button" className="inline-flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-2.5 py-2 text-xs font-medium text-slate-600 hover:border-primary hover:text-primary">
                  <Eye className="h-3.5 w-3.5" />Vista completa
                </button>
              </div>

              {matchStats.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-6 text-center text-sm text-textMuted">Aún no hay incidencias registradas para este encuentro.</div>
              ) : (
                <div className="space-y-3">
                  {matchStats.map((event) => (
                    <div key={event.id} className="flex items-start gap-3 rounded-2xl border border-slate-200 bg-slate-50 p-3">
                      <div className={`mt-1 h-3 w-3 rounded-full ${event.color ?? 'bg-primary'}`} />
                      <div className="flex-1">
                        <div className="flex items-center justify-between gap-2">
                          <span className="font-semibold text-textPrimary">{event.type}</span>
                          <span className="text-xs text-textMuted">{event.minute}</span>
                        </div>
                        <div className="mt-1 text-sm text-textMuted">{event.team} • {event.player ?? 'Equipo sancionado'}</div>
                        <div className="mt-1 text-sm text-slate-700">{event.description}</div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function TournamentsPage() {
  const [selected, setSelected] = useState<TournamentStatus | 'Todos'>('Todos');
  const [items, setItems] = useState(initialTournaments);

  const handleDelete = (id: number) => {
    setItems((prev) => prev.filter((t) => t.id !== id));
    triggerToast('Campeonato eliminado');
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div>
          <p className="text-xs uppercase tracking-[0.2em] text-textMuted">Gestión</p>
          <h2 className="text-3xl font-bold">Campeonatos</h2>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <button type="button" onClick={() => triggerToast('Filtros aplicados')} className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-600 transition hover:border-primary hover:text-primary">Filtrar</button>
          <button type="button" onClick={() => openQuickModal('Nuevo campeonato')} className="rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90">+ Nuevo campeonato</button>
        </div>
      </div>

      <div className="card-surface p-4">
        <div className="flex flex-wrap gap-2">
          {['Todos', 'En curso', 'Finalizado', 'Próximo'].map((filter) => (
            <button
              type="button"
              key={filter}
              onClick={() => setSelected(filter as TournamentStatus | 'Todos')}
              className={`rounded-full px-3 py-1.5 text-sm font-medium transition-all duration-200 ease-in-out ${selected === filter ? 'bg-primary text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}
            >
              {filter}
            </button>
          ))}
        </div>
      </div>

      <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
        {items.filter((t) => selected === 'Todos' || t.status === selected).map((tournament) => (
          <div key={tournament.id} className="card-surface overflow-hidden transition-all duration-200 ease-in-out hover:-translate-y-1 hover:shadow-md">
            <div className="bg-gradient-to-r from-primary to-secondary p-4 text-white">
              <div className="flex items-center justify-between">
                <span className="badge-pill bg-white/10 text-white">{tournament.category}</span>
                <span className={`badge-pill ${statusStyles[tournament.status]}`}>{tournament.status}</span>
              </div>
              <h3 className="mt-4 text-2xl font-bold">{tournament.name}</h3>
            </div>
            <div className="space-y-3 p-5">
              <div className="flex items-center justify-between text-sm text-textMuted"><span>Temporada</span><span className="font-semibold text-textPrimary">{tournament.season}</span></div>
              <div className="flex items-center justify-between text-sm text-textMuted"><span>Formato</span><span className="font-semibold text-textPrimary">{tournament.format}</span></div>
              <div className="flex items-center justify-between text-sm text-textMuted"><span>Equipos</span><span className="font-semibold text-textPrimary">{tournament.teams}</span></div>
              <div className="flex items-center justify-between text-sm text-textMuted"><span>Próximo partido</span><span className="font-semibold text-textPrimary">{tournament.nextMatch}</span></div>
              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => { openQuickModal('Editar campeonato'); triggerToast(`Editando ${tournament.name}`); }} className="rounded-xl border border-slate-200 p-2 text-slate-600 transition hover:border-primary hover:text-primary"><Pencil className="h-4 w-4" /></button>
                <button type="button" onClick={() => handleDelete(tournament.id)} className="rounded-xl border border-slate-200 p-2 text-slate-600 transition hover:border-red-300 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function TeamsPage() {
  const [tab, setTab] = useState<'equipos' | 'jugadores' | 'nomina'>('equipos');
  const [showCreateTeam, setShowCreateTeam] = useState(false);
  const [showCreatePlayer, setShowCreatePlayer] = useState(false);
  const [teamName, setTeamName] = useState('');
  const [teamShortName, setTeamShortName] = useState('');
  const [playerForm, setPlayerForm] = useState({ firstName: '', lastName: '', documentId: '', birthDate: '', gender: 'Masculino', guardianName: '', guardianPhone: '' });
  const [teamError, setTeamError] = useState<string | null>(null);
  const [playerError, setPlayerError] = useState<string | null>(null);
  const [isSavingPlayer, setIsSavingPlayer] = useState(false);
  const { teams: teamItems, players: playerItems, setTeams } = useAppContext();
  const { setPlayers } = useAppContext();

  const tabs = [
    { id: 'equipos', label: 'Equipos' },
    { id: 'jugadores', label: 'Jugadores' },
    { id: 'nomina', label: 'Nóminas' },
  ] as const;

  const handleCreateTeam = async () => {
    if (!teamName.trim()) {
      setTeamError('El nombre del equipo es obligatorio.');
      return;
    }

    try {
      const response = await createApiClub(teamName.trim(), teamShortName.trim());
      setTeams((current) => [...current, {
        id: current.length + 1,
        name: teamName.trim(),
        shortName: teamShortName.trim() || teamName.trim().slice(0, 3).toUpperCase(),
        category: 'Primera',
        coach: 'Sin entrenador asignado',
        players: 0,
        badgeColor: 'from-sky-500 to-cyan-500',
      }]);
      setShowCreateTeam(false);
      setTeamName('');
      setTeamShortName('');
      setTeamError(null);
      triggerToast(`Equipo creado correctamente (${response.id.slice(0, 8)})`);
    } catch (error) {
      setTeamError(error instanceof Error ? error.message : 'No se pudo crear el equipo.');
    }
  };

  const handleCreatePlayer = async () => {
    if (!playerForm.firstName.trim() || !playerForm.lastName.trim() || !playerForm.documentId.trim() || !playerForm.birthDate) {
      setPlayerError('Completa nombre, apellido, documento y fecha de nacimiento.');
      return;
    }

    setIsSavingPlayer(true);
    setPlayerError(null);
    try {
      const response = await createApiAthlete(playerForm);
      if (response.alreadyRegistered) {
        setPlayerError('Ya existe un jugador registrado con ese documento.');
        return;
      }

      setPlayers((current) => [...current, {
        id: current.length + 1,
        name: `${playerForm.firstName.trim()} ${playerForm.lastName.trim()}`,
        document: playerForm.documentId.trim(),
        birthDate: playerForm.birthDate,
        category: 'Primera',
        team: 'Sin equipo asignado',
        position: 'Jugador',
        number: current.length + 1,
        photo: '',
        goals: 0,
        assists: 0,
        yellowCards: 0,
        redCards: 0,
      }]);
      setPlayerForm({ firstName: '', lastName: '', documentId: '', birthDate: '', gender: 'Masculino', guardianName: '', guardianPhone: '' });
      setShowCreatePlayer(false);
      triggerToast('Jugador creado correctamente');
    } catch (error) {
      setPlayerError(error instanceof Error ? error.message : 'No se pudo crear el jugador.');
    } finally {
      setIsSavingPlayer(false);
    }
  };

  const handleSaveAthlete = async (values: AthleteFormValues) => {
    setIsSavingPlayer(true);
    setPlayerError(null);
    try {
      const photoUrl = values.photo ? await fileToDataUrl(values.photo) : undefined;
      const response = await createApiAthlete({
        firstName: values.firstName,
        lastName: values.lastName,
        documentId: `${values.documentId.trim()}-${values.documentExtension}`,
        birthDate: values.birthDate,
        gender: values.gender,
        guardianName: values.guardianName,
        guardianPhone: values.guardianPhone,
        photoUrl,
      });
      if (response.alreadyRegistered) {
        throw new Error('Ya existe un jugador registrado con ese documento y extensión.');
      }
      setPlayers((current) => [...current, {
        id: current.length + 1,
        name: `${values.firstName.trim()} ${values.lastName.trim()}`,
        document: `${values.documentId.trim()}-${values.documentExtension}`,
        birthDate: values.birthDate,
        category: 'Primera',
        team: values.teamId ? teamItems.find((team) => team.id === values.teamId)?.name ?? 'Sin equipo asignado' : 'Sin equipo asignado',
        position: values.position || 'Jugador',
        number: values.jerseyNumber ?? current.length + 1,
        photo: values.photo ? URL.createObjectURL(values.photo) : '',
        goals: 0,
        assists: 0,
        yellowCards: 0,
        redCards: 0,
      }]);
      setShowCreatePlayer(false);
      triggerToast('Jugador creado correctamente');
    } catch (error) {
      setPlayerError(error instanceof Error ? error.message : 'No se pudo crear el jugador.');
    } finally {
      setIsSavingPlayer(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs uppercase tracking-[0.2em] text-textMuted">Clubes y planteles</p>
          <h2 className="text-3xl font-bold">Administración de equipos</h2>
        </div>
        <button type="button" onClick={() => { if (tab === 'jugadores') { setPlayerError(null); setShowCreatePlayer(true); } else { setTeamError(null); setShowCreateTeam(true); } }} className="rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90">+ {tab === 'jugadores' ? 'Crear jugador' : 'Crear equipo'}</button>
      </div>

      <div className="card-surface p-2">
        <div className="flex flex-wrap gap-2">
          {tabs.map((item) => (
            <button
              type="button"
              key={item.id}
              onClick={() => setTab(item.id)}
              className={`rounded-xl px-4 py-2 text-sm font-medium transition-all duration-200 ease-in-out ${tab === item.id ? 'bg-primary text-white' : 'text-slate-600 hover:bg-slate-100'}`}
            >
              {item.label}
            </button>
          ))}
        </div>
      </div>

      {tab === 'equipos' && (
        <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
          {teamItems.map((team) => (
            <div key={team.id} className="card-surface p-5 transition-all duration-200 ease-in-out hover:-translate-y-1 hover:shadow-md">
              <div className={`mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br ${team.badgeColor} text-xl font-black text-white shadow-lg`}>
                {team.shortName}
              </div>
              <h3 className="text-xl font-bold">{team.name}</h3>
              <div className="mt-2 text-sm text-textMuted">Categoría: {team.category}</div>
              <div className="mt-2 text-sm text-textMuted">Entrenador: {team.coach}</div>
              <div className="mt-4 flex items-center justify-between text-sm">
                <span className="text-textMuted">Jugadores</span>
                <span className="font-semibold text-textPrimary">{team.players}</span>
              </div>
              <button type="button" onClick={() => triggerToast(`${team.name} seleccionado para edición`)} className="mt-4 w-full rounded-xl bg-primary px-3 py-2 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90">Ver detalle</button>
            </div>
          ))}
        </div>
      )}

      {tab === 'jugadores' && (
        <div className="card-surface overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-primary text-white">
                <tr>
                  <th className="px-4 py-3">Foto</th>
                  <th className="px-4 py-3">Nombre</th>
                  <th className="px-4 py-3">CI / Documento</th>
                  <th className="px-4 py-3">Nacimiento</th>
                  <th className="px-4 py-3">Categoría</th>
                </tr>
              </thead>
              <tbody>
                {playerItems.map((player) => (
                  <tr key={player.id} className="border-b border-slate-200 last:border-b-0">
                    <td className="px-4 py-3"><img src={player.photo} alt={player.name} className="h-10 w-10 rounded-full object-cover" /></td>
                    <td className="px-4 py-3 font-semibold">{player.name}</td>
                    <td className="px-4 py-3 text-textMuted">{player.document}</td>
                    <td className="px-4 py-3 text-textMuted">{player.birthDate}</td>
                    <td className="px-4 py-3"><span className="badge-pill bg-cyan-100 text-cyan-700">{player.category}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {tab === 'nomina' && (
        <div className="grid gap-6 xl:grid-cols-2">
          <div className="card-surface p-5">
            <h3 className="text-lg font-bold">Asignar jugador a equipo</h3>
            <div className="mt-4 space-y-4">
              <label className="block text-sm">
                <span className="mb-1.5 block font-medium text-slate-600">Equipo</span>
                <select className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5">
                  {teamItems.map((team) => <option key={team.id}>{team.name}</option>)}
                </select>
              </label>
              <label className="block text-sm">
                <span className="mb-1.5 block font-medium text-slate-600">Jugador</span>
                <select className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5">
                  {playerItems.map((player) => <option key={player.id}>{player.name}</option>)}
                </select>
              </label>
              <div className="grid gap-4 md:grid-cols-2">
                <label className="block text-sm">
                  <span className="mb-1.5 block font-medium text-slate-600">Número</span>
                  <input defaultValue={9} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5" />
                </label>
                <label className="block text-sm">
                  <span className="mb-1.5 block font-medium text-slate-600">Posición</span>
                  <input defaultValue="Delantero" className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5" />
                </label>
              </div>
              <button type="button" onClick={() => triggerToast('Nómina asignada correctamente')} className="w-full rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90">Guardar nómina</button>
            </div>
          </div>

          <div className="card-surface p-5">
            <h3 className="text-lg font-bold">Nóminas activas</h3>
            <div className="mt-4 space-y-3">
              {playerItems.slice(0, 4).map((player) => (
                <div key={player.id} className="flex items-center justify-between rounded-2xl bg-slate-50 p-3 transition hover:bg-slate-100">
                  <div className="flex items-center gap-3">
                    <img src={player.photo} alt={player.name} className="h-11 w-11 rounded-full object-cover" />
                    <div>
                      <div className="font-semibold">{player.name}</div>
                      <div className="text-xs text-textMuted">#{player.number} • {player.position}</div>
                    </div>
                  </div>
                  <span className="badge-pill bg-emerald-100 text-emerald-700">Activo</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {showCreateTeam && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-950/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl">
            <div className="mb-5 flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-textMuted">Clubes</p>
                <h3 className="mt-1 text-xl font-bold">Crear equipo</h3>
              </div>
              <button type="button" onClick={() => setShowCreateTeam(false)} className="rounded-full p-2 hover:bg-slate-100">×</button>
            </div>
            <div className="space-y-4">
              <label className="block text-sm">
                <span className="mb-1.5 block font-medium text-slate-600">Nombre del equipo</span>
                <input value={teamName} onChange={(event) => setTeamName(event.target.value)} placeholder="Ej. Club Deportivo Central" className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none focus:border-primary focus:bg-white" />
              </label>
              <label className="block text-sm">
                <span className="mb-1.5 block font-medium text-slate-600">Abreviatura</span>
                <input value={teamShortName} onChange={(event) => setTeamShortName(event.target.value.toUpperCase().slice(0, 20))} placeholder="CDC" className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 uppercase outline-none focus:border-primary focus:bg-white" />
              </label>
              {teamError && <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{teamError}</div>}
              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={() => setShowCreateTeam(false)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50">Cancelar</button>
                <button type="button" onClick={() => void handleCreateTeam()} className="rounded-xl bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary/90">Guardar equipo</button>
              </div>
            </div>
          </div>
        </div>
      )}

      {showCreatePlayer && (
        <AthleteModal open teams={teamItems} loading={isSavingPlayer} error={playerError} onClose={() => setShowCreatePlayer(false)} onSave={handleSaveAthlete} />
      )}

      {false && showCreatePlayer && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-950/50 p-4 backdrop-blur-sm">
          <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl">
            <div className="mb-5 flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-textMuted">Registro deportivo</p>
                <h3 className="mt-1 text-xl font-bold">Crear jugador</h3>
              </div>
              <button type="button" onClick={() => setShowCreatePlayer(false)} className="rounded-full p-2 hover:bg-slate-100">×</button>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              {[
                ['firstName', 'Nombre'],
                ['lastName', 'Apellido'],
                ['documentId', 'Documento'],
                ['birthDate', 'Fecha de nacimiento'],
              ].map(([key, label]) => (
                <label key={key} className="block text-sm">
                  <span className="mb-1.5 block font-medium text-slate-600">{label}</span>
                  <input type={key === 'birthDate' ? 'date' : 'text'} value={playerForm[key as keyof typeof playerForm]} onChange={(event) => setPlayerForm((current) => ({ ...current, [key]: event.target.value }))} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none focus:border-primary focus:bg-white" />
                </label>
              ))}
              <label className="block text-sm">
                <span className="mb-1.5 block font-medium text-slate-600">Género</span>
                <select value={playerForm.gender} onChange={(event) => setPlayerForm((current) => ({ ...current, gender: event.target.value }))} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none focus:border-primary focus:bg-white">
                  <option>Masculino</option>
                  <option>Femenino</option>
                  <option>Otro</option>
                </select>
              </label>
              <label className="block text-sm">
                <span className="mb-1.5 block font-medium text-slate-600">Nombre del tutor <span className="font-normal text-textMuted">(opcional)</span></span>
                <input value={playerForm.guardianName} onChange={(event) => setPlayerForm((current) => ({ ...current, guardianName: event.target.value }))} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none focus:border-primary focus:bg-white" />
              </label>
              <label className="block text-sm md:col-span-2">
                <span className="mb-1.5 block font-medium text-slate-600">Teléfono del tutor <span className="font-normal text-textMuted">(opcional)</span></span>
                <input value={playerForm.guardianPhone} onChange={(event) => setPlayerForm((current) => ({ ...current, guardianPhone: event.target.value }))} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none focus:border-primary focus:bg-white" />
              </label>
            </div>
            {playerError && <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{playerError}</div>}
            <div className="mt-6 flex justify-end gap-3">
              <button type="button" onClick={() => setShowCreatePlayer(false)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50">Cancelar</button>
              <button type="button" disabled={isSavingPlayer} onClick={() => void handleCreatePlayer()} className="rounded-xl bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60">{isSavingPlayer ? 'Guardando...' : 'Guardar jugador'}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function fileToDataUrl(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(new Error('No se pudo leer la fotografía.'));
    reader.readAsDataURL(file);
  });
}

function VenuesPage() {
  const [venueItems, setVenueItems] = useState(initialVenues);

  const toggleVenueAvailability = (id: number) => {
    setVenueItems((prev) => prev.map((venue) => (venue.id === id ? { ...venue, available: !venue.available } : venue)));
    triggerToast('Disponibilidad actualizada');
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs uppercase tracking-[0.2em] text-textMuted">Infraestructura</p>
          <h2 className="text-3xl font-bold">Sedes y canchas</h2>
        </div>
        <button type="button" onClick={() => openQuickModal('Registrar cancha')} className="rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90">+ Registrar cancha</button>
      </div>

      <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        {venueItems.map((venue) => (
          <div key={venue.id} className="card-surface p-5 transition-all duration-200 ease-in-out hover:-translate-y-1 hover:shadow-md">
            <div className="flex items-center justify-between">
              <div className="rounded-xl bg-primary/10 p-3 text-primary"><MapPinned className="h-5 w-5" /></div>
              <span className={`badge-pill ${venue.available ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-200 text-slate-700'}`}>{venue.available ? 'Disponible' : 'No disponible'}</span>
            </div>
            <h3 className="mt-4 text-xl font-bold">{venue.name}</h3>
            <div className="mt-2 text-sm text-textMuted">Superficie: {venue.surface}</div>
            <div className="mt-2 text-sm text-textMuted">Ubicación: {venue.location}</div>
            <button type="button" onClick={() => toggleVenueAvailability(venue.id)} className="mt-4 w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-medium text-slate-700 transition hover:border-primary hover:text-primary">
              {venue.available ? 'Desactivar disponibilidad' : 'Activar disponibilidad'}
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}

function SchedulePage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs uppercase tracking-[0.2em] text-textMuted">Calendario</p>
          <h2 className="text-3xl font-bold">Fixture por fases</h2>
        </div>
        <button type="button" onClick={() => openQuickModal('Programar partido')} className="rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90">+ Programar partido</button>
      </div>

      {['Fase de grupos', 'Cuartos', 'Semifinal', 'Final'].map((phase) => (
        <div key={phase} className="card-surface p-5">
          <h3 className="mb-4 text-lg font-bold">{phase}</h3>
          <div className="space-y-3">
            {initialMatches.filter((match) => match.phase === phase).map((match) => (
              <button
                type="button"
                key={match.id}
                onClick={() => triggerToast(`${match.homeTeam} vs ${match.awayTeam} programado`)}
                className="flex w-full flex-col gap-2 rounded-2xl border border-slate-200 bg-slate-50 p-4 text-left transition-all duration-200 ease-in-out hover:-translate-y-0.5 hover:border-primary hover:bg-white md:flex-row md:items-center md:justify-between"
              >
                <div>
                  <div className="text-sm text-textMuted">{match.date} • {match.time}</div>
                  <div className="mt-1 text-lg font-bold">{match.homeTeam} vs {match.awayTeam}</div>
                </div>
                <div className="flex items-center gap-3 text-sm text-textMuted">
                  <span>{match.court}</span>
                  <span className="h-1.5 w-1.5 rounded-full bg-primary" />
                  <span>{match.ref}</span>
                </div>
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

function LiveMatchPage() {
  const [time, setTime] = useState(1254);
  const [isRunning, setIsRunning] = useState(true);
  const [matchHasStarted, setMatchHasStarted] = useState(true);
  const [score, setScore] = useState({ home: 2, away: 1 });
  const [events, setEvents] = useState<MatchEvent[]>(initialMatchEvents);
  const [eventType, setEventType] = useState<'Gol' | 'Tarjeta Amarilla' | 'Tarjeta Roja' | 'Walkover'>('Gol');
  const [player, setPlayer] = useState('Valentina Cruz');
  const [weather, setWeather] = useState('Soleado');
  const [period, setPeriod] = useState("1T 45'");

  useEffect(() => {
    if (!isRunning) return;
    const timer = window.setInterval(() => {
      setTime((prev) => prev + 1);
    }, 1000);
    return () => window.clearInterval(timer);
  }, [isRunning]);

  const formatTime = (value: number) => {
    const minutes = Math.floor(value / 60);
    const seconds = value % 60;
    return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
  };

  const currentMinute = Math.floor(time / 60).toString();

  const handleToggleMatch = () => {
    setIsRunning((prev) => {
      const next = !prev;
      if (next) setMatchHasStarted(true);
      return next;
    });
  };

  const handleQuickScore = (team: 'home' | 'away') => {
    if (!matchHasStarted) {
      triggerToast('Primero inicia el partido para registrar goles');
      return;
    }

    const teamLabel = team === 'home' ? 'Local' : 'Visitante';
    const newEvent: MatchEvent = {
      id: Date.now(),
      minute: currentMinute,
      team: teamLabel,
      type: 'Gol',
      player: team === 'home' ? player : 'Mateo Silva',
      description: `Gol de ${team === 'home' ? player : 'Mateo Silva'}`,
      color: 'bg-emerald-500',
    };

    setScore((prev) => ({
      ...prev,
      [team]: prev[team] + 1,
    }));
    setEvents((prev) => [newEvent, ...prev]);
    triggerToast(`Gol para ${teamLabel} al minuto ${currentMinute}'`);
  };

  const handleSaveEvent = () => {
    if (!matchHasStarted) {
      triggerToast('Primero inicia el partido para registrar eventos');
      return;
    }

    if (eventType === 'Gol') {
      setScore((prev) => ({ ...prev, home: prev.home + 1 }));
    }

    const newEvent: MatchEvent = {
      id: Date.now(),
      minute: currentMinute,
      team: 'Local',
      type: eventType,
      player: eventType === 'Walkover' ? undefined : player,
      description: `${eventType} de ${eventType === 'Walkover' ? 'equipo local' : player}`,
      color: eventType === 'Gol' ? 'bg-emerald-500' : eventType === 'Tarjeta Amarilla' ? 'bg-yellow-400' : eventType === 'Tarjeta Roja' ? 'bg-red-500' : 'bg-violet-500',
    };
    setEvents((prev) => [newEvent, ...prev]);
    triggerToast(`Evento registrado al minuto ${currentMinute}'`);
  };

  return (
    <div className="space-y-6">
      <div className="card-surface overflow-hidden bg-gradient-to-br from-primary to-secondary text-white shadow-soft">
        <div className="flex flex-col gap-4 p-6 md:flex-row md:items-center md:justify-between">
          <div>
            <div className="mb-2 flex items-center gap-2">
              <span className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-sky-100">
                <span className="h-2 w-2 rounded-full bg-emerald-400 animate-pulse" />
                {(isRunning ? 'En vivo' : 'Pausado')}
              </span>
            </div>
            <div className="text-xs uppercase tracking-[0.25em] text-sky-100">Centro de control</div>
            <h2 className="mt-2 text-3xl font-bold">Club Atlético Norte vs Sporting Sur</h2>
          </div>
          <div className="flex items-center gap-3">
            <button type="button" onClick={handleToggleMatch} className="rounded-xl bg-white/15 p-3 text-white transition hover:bg-white/20">
              {isRunning ? <Pause className="h-5 w-5" /> : <CirclePlay className="h-5 w-5" />}
            </button>
            <button type="button" onClick={() => setTime(0)} className="rounded-xl bg-white/15 p-3 text-white transition hover:bg-white/20"><TimerReset className="h-5 w-5" /></button>
            <div className="rounded-xl bg-white/10 px-4 py-2 text-2xl font-black">{formatTime(time)}</div>
          </div>
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
        <div className="space-y-6">
          <div className="card-surface p-5">
            <div className="mb-4 flex items-center justify-between">
              <h3 className="text-lg font-bold">Eventos del partido</h3>
              <span className="badge-pill bg-emerald-100 text-emerald-700">{weather}</span>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <label className="block text-sm font-medium text-slate-600">Periodo</label>
                <select value={period} onChange={(event) => setPeriod(event.target.value)} className="mt-1 w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5">
                  <option>1T 45'</option>
                  <option>2T 45'</option>
                  <option>Extra 15'+15'</option>
                  <option>Penales</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-600">Clima</label>
                <select value={weather} onChange={(event) => setWeather(event.target.value)} className="mt-1 w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5">
                  <option>Soleado</option>
                  <option>Lluvia</option>
                  <option>Nublado</option>
                </select>
              </div>
            </div>

            <div className="mt-5 rounded-2xl border border-slate-200 bg-slate-50 p-4">
              <div className="mb-3 flex items-center justify-between text-sm text-textMuted">
                <span>Marcador</span>
                <span className="font-semibold text-textPrimary">{period}</span>
              </div>
              <div className="flex items-center justify-between gap-3">
                <div className="text-center">
                  <div className="text-xs uppercase tracking-[0.2em] text-textMuted">Local</div>
                  <div className="mt-2 text-3xl font-black text-primary">{score.home}</div>
                  <div className="mt-2 flex items-center justify-center gap-2">
                    <button type="button" onClick={() => handleQuickScore('home')} className="rounded-lg bg-primary px-2 py-1 text-xs font-semibold text-white">+ Gol</button>
                  </div>
                </div>
                <div className="text-2xl font-black text-slate-400">:</div>
                <div className="text-center">
                  <div className="text-xs uppercase tracking-[0.2em] text-textMuted">Visitante</div>
                  <div className="mt-2 text-3xl font-black text-secondary">{score.away}</div>
                  <div className="mt-2 flex items-center justify-center gap-2">
                    <button type="button" onClick={() => handleQuickScore('away')} className="rounded-lg bg-secondary px-2 py-1 text-xs font-semibold text-white">+ Gol</button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div className="card-surface p-5">
            <h3 className="text-lg font-bold">Registrar evento</h3>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <div>
                <label className="block text-sm font-medium text-slate-600">Tipo</label>
                <select value={eventType} onChange={(event) => setEventType(event.target.value as 'Gol' | 'Tarjeta Amarilla' | 'Tarjeta Roja' | 'Walkover')} className="mt-1 w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5">
                  <option>Gol</option>
                  <option>Tarjeta Amarilla</option>
                  <option>Tarjeta Roja</option>
                  <option>Walkover</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-600">Minuto</label>
                <input
                  value={currentMinute}
                  readOnly
                  className="mt-1 w-full rounded-xl border border-slate-200 bg-slate-100 px-3 py-2.5 text-slate-500 shadow-inner outline-none"
                />
              </div>
              <div className="md:col-span-2">
                <label className="block text-sm font-medium text-slate-600">Jugador</label>
                <input value={player} onChange={(event) => setPlayer(event.target.value)} className="mt-1 w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5" />
              </div>
            </div>
            <button type="button" onClick={handleSaveEvent} className="mt-4 w-full rounded-xl bg-accent px-4 py-2.5 text-sm font-semibold text-slate-900 transition hover:-translate-y-0.5 hover:bg-cyan-400">Guardar evento</button>
          </div>
        </div>

        <div className="card-surface p-5">
          <h3 className="text-lg font-bold">Cronología</h3>
          <div className="mt-4 space-y-4">
            {events.map((event) => (
              <div key={event.id} className="relative pl-6">
                <div className="absolute left-0 top-1.5 h-3 w-3 rounded-full bg-primary" />
                <div className="rounded-2xl border border-slate-200 bg-slate-50 p-3">
                  <div className="flex items-center justify-between text-xs text-textMuted">
                    <span>{event.minute}'</span>
                    <span className="badge-pill bg-primary/10 text-primary">{event.team}</span>
                  </div>
                  <div className="mt-2 font-semibold">{event.type}</div>
                  <div className="mt-1 text-sm text-textMuted">{event.description}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function StatsPage() {
  const exportCsv = () => {
    const rows = [
      ['Jugador', 'Equipo', 'Goles', 'Asistencias', 'Amarillas', 'Rojas'],
      ...initialPlayers.map((player) => [player.name, player.team, String(player.goals), String(player.assists), String(player.yellowCards), String(player.redCards)]),
    ];

    const csvContent = rows.map((row) => row.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'sportfrog-indicadores.csv';
    link.click();
    URL.revokeObjectURL(url);
    triggerToast('CSV exportado correctamente');
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs uppercase tracking-[0.2em] text-textMuted">Indicadores</p>
          <h2 className="text-3xl font-bold">Reporte individual</h2>
        </div>
        <button type="button" onClick={exportCsv} className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90"><Download className="h-4 w-4" /> Exportar CSV</button>
      </div>

      <div className="grid gap-5 lg:grid-cols-3">
        {[
          { name: 'Mateo Silva', goals: 12, team: 'Sporting Sur', medal: '🥇' },
          { name: 'Sofía Romero', goals: 9, team: 'Real Horizonte', medal: '🥈' },
          { name: 'Valentina Cruz', goals: 14, team: 'Club Atlético Norte', medal: '🥉' },
        ].map((player, index) => (
          <div key={player.name} className={`card-surface p-5 transition-all duration-200 ease-in-out hover:-translate-y-1 ${index === 1 ? 'ring-2 ring-primary/20' : ''}`}>
            <div className="text-3xl">{player.medal}</div>
            <div className="mt-4 flex items-center gap-3">
              <img src={initialPlayers[index]?.photo} alt={player.name} className="h-12 w-12 rounded-full object-cover" />
              <div>
                <div className="font-bold text-lg">{player.name}</div>
                <div className="text-sm text-textMuted">{player.team}</div>
              </div>
            </div>
            <div className="mt-4 text-3xl font-black text-primary">{player.goals}</div>
            <div className="text-sm text-textMuted">Goles</div>
          </div>
        ))}
      </div>

      <div className="card-surface overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-primary text-white">
              <tr>
                <th className="px-4 py-3">Foto</th>
                <th className="px-4 py-3">Jugador</th>
                <th className="px-4 py-3">Equipo</th>
                <th className="px-4 py-3">Goles</th>
                <th className="px-4 py-3">Asistencias</th>
                <th className="px-4 py-3">Amarillas</th>
                <th className="px-4 py-3">Rojas</th>
              </tr>
            </thead>
            <tbody>
              {initialPlayers.map((player) => (
                <tr key={player.id} className="border-b border-slate-200 last:border-b-0">
                  <td className="px-4 py-3"><img src={player.photo} alt={player.name} className="h-10 w-10 rounded-full object-cover" /></td>
                  <td className="px-4 py-3 font-semibold">{player.name}</td>
                  <td className="px-4 py-3 text-textMuted">{player.team}</td>
                  <td className="px-4 py-3">{player.goals}</td>
                  <td className="px-4 py-3">{player.assists}</td>
                  <td className="px-4 py-3">{player.yellowCards}</td>
                  <td className="px-4 py-3">{player.redCards}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function CredentialEditorPage() {
  const [selectedProperty, setSelectedProperty] = useState('Foto');
  const credential = {
    name: initialPlayers[0].name,
    role: 'Delantera',
    team: initialPlayers[0].team,
    category: initialPlayers[0].category,
    number: 9,
    id: 'SF-2048',
    expiry: '31/12/2026',
  };

  const propertyItems = ['Foto', 'Nombre', 'Dorsal', 'Equipo', 'Categoría', 'Código QR'];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-[0.2em] text-textMuted">Credenciales</p>
          <h2 className="text-3xl font-bold">Gestión de identidad</h2>
        </div>
        <div className="flex items-center gap-2">
          <button type="button" onClick={() => triggerToast('Vista previa actualizada')} className="rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-sm font-medium text-slate-600 transition hover:border-primary hover:text-primary">Vista previa</button>
          <button type="button" onClick={() => triggerToast('Credencial publicada')} className="rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition hover:-translate-y-0.5 hover:bg-primary/90">Publicar</button>
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.25fr_0.75fr]">
        <div className="card-surface p-5">
          <div className="relative overflow-hidden rounded-[32px] border border-slate-200 bg-[radial-gradient(circle_at_top,_rgba(14,165,233,0.30),_rgba(15,23,42,0.96)_45%,_rgba(2,6,23,1)_100%)] p-5 text-white shadow-2xl">
            <div className="absolute inset-x-0 top-0 h-28 bg-gradient-to-r from-primary/40 via-sky-400/20 to-secondary/30 blur-2xl" />

            <div className="relative">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-white/10 text-lg font-black text-white shadow-lg ring-1 ring-white/20">SF</div>
                  <div>
                    <div className="font-display text-2xl font-bold">SportFrog</div>
                    <div className="text-[10px] uppercase tracking-[0.25em] text-sky-100">Identity pass</div>
                  </div>
                </div>
                <div className="rounded-full border border-white/15 bg-white/10 px-3 py-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-sky-100">
                  Válida {credential.expiry}
                </div>
              </div>

              <div className="mt-6 grid gap-5 md:grid-cols-[0.9fr_1.1fr]">
                <div className="flex items-center justify-center md:justify-start">
                  <div className="relative">
                    <img src={initialPlayers[0].photo} alt={credential.name} className="h-36 w-28 rounded-[24px] object-cover ring-4 ring-white/20 shadow-2xl" />
                    <div className="absolute -bottom-3 left-1/2 -translate-x-1/2 rounded-full bg-accent px-2.5 py-1 text-xs font-bold text-slate-900 shadow-md">#{credential.number}</div>
                  </div>
                </div>

                <div className="space-y-3">
                  <span className="inline-flex rounded-full border border-emerald-300/60 bg-emerald-400/10 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-emerald-200">{credential.category}</span>
                  <div>
                    <div className="text-3xl font-black tracking-tight text-white">{credential.name}</div>
                    <div className="mt-1 text-xs uppercase tracking-[0.28em] text-slate-200">{credential.role}</div>
                  </div>

                  <div className="space-y-2 text-sm text-slate-200">
                    <div className="flex items-center justify-between rounded-xl border border-white/10 bg-white/5 px-3 py-2">
                      <span>Equipo</span>
                      <span className="font-semibold text-white">{credential.team}</span>
                    </div>
                    <div className="flex items-center justify-between rounded-xl border border-white/10 bg-white/5 px-3 py-2">
                      <span>Documento</span>
                      <span className="font-semibold text-white">{initialPlayers[0].document}</span>
                    </div>
                    <div className="flex items-center justify-between rounded-xl border border-white/10 bg-white/5 px-3 py-2">
                      <span>Tipo</span>
                      <span className="font-semibold text-white">Jugador oficial</span>
                    </div>
                  </div>
                </div>
              </div>

              <div className="mt-6 flex items-end justify-between gap-4 border-t border-white/10 pt-4">
                <div>
                  <div className="text-[10px] uppercase tracking-[0.2em] text-sky-100">ID credencial</div>
                  <div className="mt-1 text-xl font-bold text-white">{credential.id}</div>
                </div>

                <div className="flex items-center gap-3">
                  <div className="rounded-xl border border-white/15 bg-white/5 p-2">
                    <QrCode className="h-8 w-8 text-white" />
                  </div>
                  <div className="text-center">
                    <div className="text-[10px] uppercase tracking-[0.2em] text-sky-100">Firma</div>
                    <div className="mt-1 h-8 w-20 rounded-md border border-dashed border-white/30 bg-white/5" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="card-surface p-5">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-bold">Propiedades</h3>
            <span className="badge-pill bg-primary/10 text-primary">Activo</span>
          </div>

          <div className="mt-4 space-y-3">
            {propertyItems.map((item) => (
              <div key={item} className={`flex items-center justify-between rounded-2xl border p-3 transition-all duration-200 ${selectedProperty === item ? 'border-primary bg-cyan-50 shadow-sm' : 'border-slate-200 bg-slate-50'}`}>
                <div>
                  <div className="font-medium text-slate-700">{item}</div>
                  <div className="text-xs text-textMuted">{item === 'Foto' ? 'Retrato profesional' : item === 'Nombre' ? 'Nombre legal del titular' : item === 'Dorsal' ? 'Número oficial del jugador' : item === 'Equipo' ? 'Club o asociación' : item === 'Categoría' ? 'Rama competitiva' : 'Acceso digital de validación'}</div>
                </div>
                <div className="flex items-center gap-2">
                  <button type="button" onClick={() => { setSelectedProperty(item); triggerToast(`${item} seleccionado`); }} className="rounded-xl border border-slate-200 bg-white p-2 transition hover:border-primary hover:text-primary"><Pencil className="h-3.5 w-3.5" /></button>
                  <button type="button" onClick={() => triggerToast(`${item} eliminado`)} className="rounded-xl border border-slate-200 bg-white p-2 transition hover:border-red-300 hover:text-red-600"><Trash2 className="h-3.5 w-3.5" /></button>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-6 rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-4">
            <div className="text-xs uppercase tracking-[0.2em] text-textMuted">Ajustes rápidos</div>
            <div className="mt-3 flex flex-wrap gap-2">
              <button type="button" onClick={() => triggerToast('Formato actualizado')} className="rounded-xl bg-primary px-3 py-2 text-xs font-medium text-white">Actualizar</button>
              <button type="button" onClick={() => triggerToast('Archivo exportado')} className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-600">Exportar PNG</button>
              <button type="button" onClick={() => triggerToast('Impresión lista')} className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-600">Imprimir</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function PublicPortalPage() {
  const [tab, setTab] = useState<'tabla' | 'fixture' | 'goleadores'>('tabla');

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-5">
          <div>
            <div className="font-display text-2xl font-bold text-primary">SportFrog</div>
            <div className="text-xs uppercase tracking-[0.2em] text-textMuted">Portal público</div>
          </div>
          <div className="flex items-center gap-3 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-medium text-slate-600">
            <CalendarDays className="h-4 w-4" />
            Temporada 2026
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-7xl px-6 py-8">
        <div className="card-surface p-2">
          <div className="flex flex-wrap gap-2">
            {[
              ['tabla', 'Tabla de posiciones'],
              ['fixture', 'Fixture y resultados'],
              ['goleadores', 'Goleadores'],
            ].map(([id, label]) => (
              <button
                type="button"
                key={id}
                onClick={() => setTab(id as 'tabla' | 'fixture' | 'goleadores')}
                className={`rounded-xl px-4 py-2 text-sm font-medium transition-all duration-200 ease-in-out ${tab === id ? 'bg-primary text-white' : 'text-slate-600 hover:bg-slate-100'}`}
              >
                {label}
              </button>
            ))}
          </div>
        </div>

        {tab === 'tabla' && (
          <div className="mt-6 card-surface overflow-hidden">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-primary text-white">
                <tr>
                  <th className="px-4 py-3">Equipo</th>
                  <th className="px-4 py-3">PJ</th>
                  <th className="px-4 py-3">PG</th>
                  <th className="px-4 py-3">PE</th>
                  <th className="px-4 py-3">PP</th>
                  <th className="px-4 py-3">GF</th>
                  <th className="px-4 py-3">GC</th>
                  <th className="px-4 py-3">DG</th>
                  <th className="px-4 py-3">PTS</th>
                </tr>
              </thead>
              <tbody>
                {initialTeams.map((team, idx) => (
                  <tr key={team.id} className="border-b border-slate-200 last:border-b-0">
                    <td className="px-4 py-3 font-semibold">{idx + 1}. {team.name}</td>
                    <td className="px-4 py-3">{10 + idx}</td>
                    <td className="px-4 py-3">{6 + idx}</td>
                    <td className="px-4 py-3">{2}</td>
                    <td className="px-4 py-3">{2 - idx}</td>
                    <td className="px-4 py-3">{18 + idx * 2}</td>
                    <td className="px-4 py-3">{8 + idx}</td>
                    <td className="px-4 py-3">{10 + idx}</td>
                    <td className="px-4 py-3 font-bold text-primary">{18 + idx * 4}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {tab === 'fixture' && (
          <div className="mt-6 grid gap-5 md:grid-cols-2">
            {initialMatches.map((match) => (
              <div key={match.id} className="card-surface p-5 transition hover:-translate-y-1">
                <div className="mb-3 flex items-center justify-between text-xs text-textMuted">
                  <span>{match.phase}</span>
                  <span>{match.date}</span>
                </div>
                <div className="flex items-center justify-between text-lg font-bold">
                  <span>{match.homeTeam}</span>
                  <span className="text-primary">{match.homeScore}</span>
                </div>
                <div className="mt-2 flex items-center justify-between text-lg font-bold">
                  <span>{match.awayTeam}</span>
                  <span className="text-secondary">{match.awayScore}</span>
                </div>
              </div>
            ))}
          </div>
        )}

        {tab === 'goleadores' && (
          <div className="mt-6 card-surface overflow-hidden">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-primary text-white">
                <tr>
                  <th className="px-4 py-3">Jugador</th>
                  <th className="px-4 py-3">Equipo</th>
                  <th className="px-4 py-3">Goles</th>
                  <th className="px-4 py-3">Asistencias</th>
                </tr>
              </thead>
              <tbody>
                {initialPlayers.slice(0, 5).map((player) => (
                  <tr key={player.id} className="border-b border-slate-200 last:border-b-0">
                    <td className="px-4 py-3 font-semibold">{player.name}</td>
                    <td className="px-4 py-3 text-textMuted">{player.team}</td>
                    <td className="px-4 py-3">{player.goals}</td>
                    <td className="px-4 py-3">{player.assists}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

export default function App() {
  return (
    <Routes>
      <Route element={<AdminLayout><DashboardPage /></AdminLayout>} path="/" />
      <Route element={<AdminLayout><TournamentsPage /></AdminLayout>} path="/tournaments" />
      <Route element={<AdminLayout><TeamsPage /></AdminLayout>} path="/teams" />
      <Route element={<AdminLayout><VenuesPage /></AdminLayout>} path="/venues" />
      <Route element={<AdminLayout><SchedulePage /></AdminLayout>} path="/schedule" />
      <Route element={<AdminLayout><LiveMatchPage /></AdminLayout>} path="/live" />
      <Route element={<AdminLayout><StatsPage /></AdminLayout>} path="/stats" />
      <Route element={<AdminLayout><CredentialEditorPage /></AdminLayout>} path="/credentials" />
      <Route element={<PublicPortalPage />} path="/portal" />
    </Routes>
  );
}
