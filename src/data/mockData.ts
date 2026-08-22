import type { Match, MatchEvent, Player, Team, Tournament, Venue } from '../types';

export const tournaments: Tournament[] = [
  { id: 1, name: 'Liga Metropolitana', season: '2026', category: 'Primera', format: 'Todos contra todos', status: 'En curso', teams: 12, nextMatch: '26 Ago • 19:00' },
  { id: 2, name: 'Copa Primavera', season: '2026', category: 'Sub-20', format: 'Eliminación directa', status: 'Próximo', teams: 8, nextMatch: '29 Ago • 18:30' },
  { id: 3, name: 'Torneo Federal', season: '2025', category: 'Femenino', format: 'Grupos + Final', status: 'Finalizado', teams: 10, nextMatch: 'Finalizada' },
  { id: 4, name: 'Desafío Juvenil', season: '2026', category: 'Sub-17', format: 'Zona única', status: 'En curso', teams: 6, nextMatch: '27 Ago • 17:00' },
];

export const teams: Team[] = [
  { id: 1, name: 'Club Atlético Norte', shortName: 'CAN', category: 'Primera', coach: 'Martín Varela', players: 24, badgeColor: 'from-sky-500 to-cyan-500' },
  { id: 2, name: 'Sporting Sur', shortName: 'SS', category: 'Sub-20', coach: 'Lucía Paredes', players: 21, badgeColor: 'from-emerald-500 to-teal-500' },
  { id: 3, name: 'Real Horizonte', shortName: 'RH', category: 'Femenino', coach: 'Ana Salas', players: 19, badgeColor: 'from-violet-500 to-indigo-500' },
  { id: 4, name: 'Independiente Oeste', shortName: 'IO', category: 'Sub-17', coach: 'Ramón López', players: 18, badgeColor: 'from-amber-500 to-orange-500' },
];

export const players: Player[] = [
  { id: 1, name: 'Mateo Silva', document: '45678901', birthDate: '2005-04-17', category: 'Sub-20', team: 'Sporting Sur', position: 'Delantero', number: 9, photo: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=200&q=80', goals: 12, assists: 7, yellowCards: 2, redCards: 0 },
  { id: 2, name: 'Sofía Romero', document: '45871236', birthDate: '2003-08-09', category: 'Femenino', team: 'Real Horizonte', position: 'Mediocampista', number: 8, photo: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=200&q=80', goals: 9, assists: 11, yellowCards: 3, redCards: 0 },
  { id: 3, name: 'Daniel Flores', document: '41236587', birthDate: '2004-11-22', category: 'Sub-17', team: 'Independiente Oeste', position: 'Defensor', number: 4, photo: 'https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=200&q=80', goals: 2, assists: 4, yellowCards: 1, redCards: 0 },
  { id: 4, name: 'Valentina Cruz', document: '47896521', birthDate: '1999-03-15', category: 'Primera', team: 'Club Atlético Norte', position: 'Delantera', number: 7, photo: 'https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=200&q=80', goals: 14, assists: 8, yellowCards: 2, redCards: 1 },
  { id: 5, name: 'Sebastián Rojas', document: '46621198', birthDate: '2002-02-11', category: 'Primera', team: 'Club Atlético Norte', position: 'Volante', number: 10, photo: 'https://images.unsplash.com/photo-1504593811423-6dd665756598?auto=format&fit=crop&w=200&q=80', goals: 6, assists: 13, yellowCards: 4, redCards: 0 },
  { id: 6, name: 'Lucía Vega', document: '43214567', birthDate: '2001-01-08', category: 'Femenino', team: 'Real Horizonte', position: 'Arquera', number: 1, photo: 'https://images.unsplash.com/photo-1487412720507-e7ab37603c6f?auto=format&fit=crop&w=200&q=80', goals: 0, assists: 1, yellowCards: 1, redCards: 0 },
];

export const venues: Venue[] = [
  { id: 1, name: 'Estadio Nacional', surface: 'Césped natural', location: 'San Martín', available: true },
  { id: 2, name: 'Cancha del Parque', surface: 'Sintético', location: 'Centro', available: true },
  { id: 3, name: 'Arena Sur', surface: 'Parquet', location: 'Norte', available: false },
  { id: 4, name: 'Complejo Los Pinos', surface: 'Sintético', location: 'Este', available: true },
];

export const matches: Match[] = [
  { id: 1, phase: 'Fase de grupos', date: '2026-08-26', time: '19:00', court: 'Estadio Nacional', homeTeam: 'Club Atlético Norte', awayTeam: 'Sporting Sur', homeScore: 2, awayScore: 1, status: 'En vivo', ref: 'Diego Ramírez', assistants: ['Pablo Ortiz', 'Nicolás Leiva'] },
  { id: 2, phase: 'Cuartos', date: '2026-08-28', time: '18:30', court: 'Cancha del Parque', homeTeam: 'Real Horizonte', awayTeam: 'Independiente Oeste', homeScore: 0, awayScore: 0, status: 'Programado', ref: 'Carla Mena', assistants: ['Mateo Ruiz', 'Sergio Vidal'] },
  { id: 3, phase: 'Semifinal', date: '2026-08-30', time: '20:00', court: 'Complejo Los Pinos', homeTeam: 'Club Atlético Norte', awayTeam: 'Real Horizonte', homeScore: 3, awayScore: 2, status: 'Finalizado', ref: 'Adrián Suárez', assistants: ['Juan Torres', 'Luis Ortega'] },
  { id: 4, phase: 'Final', date: '2026-09-03', time: '21:00', court: 'Estadio Nacional', homeTeam: 'Sporting Sur', awayTeam: 'Independiente Oeste', homeScore: 1, awayScore: 1, status: 'Programado', ref: 'Roberto Linares', assistants: ['Felipe Peña', 'Gustavo Díaz'] },
];

export const matchEvents: MatchEvent[] = [
  { id: 1, minute: '12', team: 'Local', type: 'Gol', player: 'Valentina Cruz', description: 'Gol de Valentina Cruz', color: 'bg-emerald-500' },
  { id: 2, minute: '29', team: 'Visitante', type: 'Tarjeta Amarilla', player: 'Mateo Silva', description: 'Tarjeta amarilla para Mateo Silva', color: 'bg-yellow-400' },
  { id: 3, minute: '45+2', team: 'Local', type: 'Gol', player: 'Sebastián Rojas', description: 'Gol de Sebastián Rojas', color: 'bg-cyan-500' },
  { id: 4, minute: '58', team: 'Visitante', type: 'Tarjeta Roja', player: 'Mateo Silva', description: 'Expulsión de Mateo Silva', color: 'bg-red-500' },
  { id: 5, minute: '72', team: 'Local', type: 'Walkover', description: 'Walkover administrativo por ausencia del equipo visitante', color: 'bg-violet-500' },
];
