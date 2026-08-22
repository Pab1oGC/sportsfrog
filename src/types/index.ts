export type TournamentStatus = 'En curso' | 'Finalizado' | 'Próximo';
export type TeamCategory = 'Sub-17' | 'Sub-20' | 'Primera' | 'Femenino' | 'Amateur';
export type SurfaceType = 'Césped natural' | 'Sintético' | 'Parquet';
export type MatchPhase = 'Fase de grupos' | 'Cuartos' | 'Semifinal' | 'Final';
export type WeatherType = 'Soleado' | 'Nublado' | 'Lluvia' | 'Ventoso';

export interface Tournament {
  id: number;
  name: string;
  season: string;
  category: string;
  format: string;
  status: TournamentStatus;
  teams: number;
  nextMatch: string;
}

export interface Team {
  id: number;
  name: string;
  shortName: string;
  category: TeamCategory;
  coach: string;
  players: number;
  badgeColor: string;
}

export interface Player {
  id: number;
  name: string;
  document: string;
  birthDate: string;
  category: TeamCategory;
  team: string;
  position: string;
  number: number;
  photo: string;
  goals: number;
  assists: number;
  yellowCards: number;
  redCards: number;
}

export interface Venue {
  id: number;
  name: string;
  surface: SurfaceType;
  location: string;
  available: boolean;
}

export interface Match {
  id: number;
  phase: MatchPhase;
  date: string;
  time: string;
  court: string;
  homeTeam: string;
  awayTeam: string;
  homeScore: number;
  awayScore: number;
  status: 'Programado' | 'En vivo' | 'Finalizado';
  ref: string;
  assistants: string[];
}

export interface MatchEvent {
  id: number;
  minute: string;
  team: 'Local' | 'Visitante';
  type: 'Gol' | 'Tarjeta Amarilla' | 'Tarjeta Roja' | 'Walkover';
  player?: string;
  description: string;
  color?: string;
}
