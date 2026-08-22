import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { matches, players, teams, tournaments, venues } from '../data/mockData';
import { CATEGORY_PRESETS, SPORT_CONFIGS } from '../data/sportConfigs';
import { getApiAthletes, getApiClubs } from '../services/api';
import type { Match, Player, Team, Tournament, Venue } from '../types';
import type { Category, SportCode } from '../types/sport';

interface AppContextValue {
  tournaments: Tournament[];
  teams: Team[];
  players: Player[];
  venues: Venue[];
  matches: Match[];
  sportConfigs: typeof SPORT_CONFIGS;
  categories: Category[];
  activeSport: SportCode;
  setTournaments: React.Dispatch<React.SetStateAction<Tournament[]>>;
  setTeams: React.Dispatch<React.SetStateAction<Team[]>>;
  setPlayers: React.Dispatch<React.SetStateAction<Player[]>>;
  setVenues: React.Dispatch<React.SetStateAction<Venue[]>>;
  setMatches: React.Dispatch<React.SetStateAction<Match[]>>;
  setActiveSport: React.Dispatch<React.SetStateAction<SportCode>>;
}

const AppContext = createContext<AppContextValue | undefined>(undefined);

const STORAGE_KEY = 'sportfrog-app-state';

function loadStoredState() {
  if (typeof window === 'undefined') {
    return null;
  }

  const raw = window.localStorage.getItem(STORAGE_KEY);
  if (!raw) return null;

  try {
    return JSON.parse(raw) as {
      tournaments?: Tournament[];
      teams?: Team[];
      players?: Player[];
      venues?: Venue[];
      matches?: Match[];
      activeSport?: SportCode;
    };
  } catch {
    return null;
  }
}

export function AppProvider({ children }: { children: React.ReactNode }) {
  const storedState = loadStoredState();

  const [tournamentList, setTournaments] = useState<Tournament[]>(storedState?.tournaments ?? tournaments);
  const [teamList, setTeams] = useState<Team[]>(storedState?.teams ?? teams);
  const [playerList, setPlayers] = useState<Player[]>(storedState?.players ?? players);
  const [venueList, setVenues] = useState<Venue[]>(storedState?.venues ?? venues);
  const [matchList, setMatches] = useState<Match[]>(storedState?.matches ?? matches);
  const [activeSport, setActiveSport] = useState<SportCode>(storedState?.activeSport ?? 'FOOTBALL');

  useEffect(() => {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        tournaments: tournamentList,
        teams: teamList,
        players: playerList,
        venues: venueList,
        matches: matchList,
        activeSport,
      }),
    );
  }, [tournamentList, teamList, playerList, venueList, matchList, activeSport]);

  useEffect(() => {
    let cancelled = false;

    async function loadApiData() {
      if (!localStorage.getItem('sportfrog_access_token') || !localStorage.getItem('sportfrog_organization_id')) {
        return;
      }

      try {
        const [apiClubs, apiAthletes] = await Promise.all([getApiClubs(), getApiAthletes()]);
        if (cancelled) return;

        setTeams(apiClubs.map((club, index) => ({
          id: index + 1,
          name: club.name,
          shortName: club.shortName ?? club.name.slice(0, 3).toUpperCase(),
          category: 'Primera',
          coach: 'Sin entrenador asignado',
          players: 0,
          badgeColor: index % 2 === 0 ? 'from-sky-500 to-cyan-500' : 'from-emerald-500 to-teal-500',
        })));

        setPlayers(apiAthletes.map((athlete, index) => ({
          id: index + 1,
          name: `${athlete.firstName} ${athlete.lastName}`,
          document: athlete.documentId,
          birthDate: athlete.birthDate,
          category: 'Primera',
          team: 'Sin equipo asignado',
          position: 'Jugador',
          number: index + 1,
          photo: athlete.photoUrl ?? '',
          goals: 0,
          assists: 0,
          yellowCards: 0,
          redCards: 0,
        })));
      } catch {
        // Demo data remains available when the API is temporarily offline.
      }
    }

    const handleSessionChanged = () => {
      void loadApiData();
    };

    void loadApiData();
    window.addEventListener('sf:api-session-changed', handleSessionChanged);
    return () => {
      cancelled = true;
      window.removeEventListener('sf:api-session-changed', handleSessionChanged);
    };
  }, []);

  const value = useMemo<AppContextValue>(
    () => ({
      tournaments: tournamentList,
      teams: teamList,
      players: playerList,
      venues: venueList,
      matches: matchList,
      sportConfigs: SPORT_CONFIGS,
      categories: CATEGORY_PRESETS,
      activeSport,
      setTournaments,
      setTeams,
      setPlayers,
      setVenues,
      setMatches,
      setActiveSport,
    }),
    [tournamentList, teamList, playerList, venueList, matchList, activeSport],
  );

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
}

export function useAppContext() {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useAppContext must be used within AppProvider');
  }
  return context;
}
