import type { Category, SportCode, SportRuleConfig } from '../types/sport';

export const SPORT_CONFIGS: Record<SportCode, SportRuleConfig> = {
  FOOTBALL: {
    code: 'FOOTBALL',
    name: 'Fútbol',
    scoreMode: 'GOALS',
    defaultPeriods: 2,
    defaultPeriodMinutes: 45,
    timerDirection: 'COUNT_UP',
    hasAddedTime: true,
    metrics: [
      { code: 'GOAL', label: 'Gol', affectsScore: true, pointsValue: 1 },
      { code: 'YELLOW_CARD', label: 'Tarjeta Amarilla', affectsScore: false },
      { code: 'RED_CARD', label: 'Tarjeta Roja', affectsScore: false },
    ],
  },
  FUTSAL: {
    code: 'FUTSAL',
    name: 'Futsal',
    scoreMode: 'GOALS',
    defaultPeriods: 2,
    defaultPeriodMinutes: 20,
    timerDirection: 'COUNT_DOWN',
    hasAddedTime: false,
    metrics: [
      { code: 'GOAL', label: 'Gol', affectsScore: true, pointsValue: 1 },
      { code: 'TEAM_FOUL', label: 'Falta Acumulada', affectsScore: false },
      { code: 'YELLOW_CARD', label: 'Tarjeta Amarilla', affectsScore: false },
      { code: 'RED_CARD', label: 'Tarjeta Roja', affectsScore: false },
    ],
  },
  BASKETBALL: {
    code: 'BASKETBALL',
    name: 'Básquetbol',
    scoreMode: 'POINTS',
    defaultPeriods: 4,
    defaultPeriodMinutes: 10,
    timerDirection: 'COUNT_DOWN',
    hasAddedTime: false,
    metrics: [
      { code: 'FREE_THROW', label: 'Tiro Libre (+1)', affectsScore: true, pointsValue: 1 },
      { code: 'FIELD_GOAL_2', label: 'Doble (+2)', affectsScore: true, pointsValue: 2 },
      { code: 'THREE_POINTER', label: 'Triple (+3)', affectsScore: true, pointsValue: 3 },
      { code: 'PERSONAL_FOUL', label: 'Falta Personal', affectsScore: false },
    ],
  },
  VOLLEYBALL: {
    code: 'VOLLEYBALL',
    name: 'Voleibol',
    scoreMode: 'SETS',
    defaultPeriods: 5,
    defaultPeriodMinutes: 0,
    timerDirection: 'ELAPSED_ONLY',
    hasAddedTime: false,
    metrics: [
      { code: 'POINT', label: 'Punto Set', affectsScore: true, pointsValue: 1 },
      { code: 'ACE', label: 'Punto de Saque (Ace)', affectsScore: true, pointsValue: 1 },
      { code: 'BLOCK', label: 'Punto de Bloqueo', affectsScore: true, pointsValue: 1 },
    ],
  },
  WALLY: {
    code: 'WALLY',
    name: 'Wally',
    scoreMode: 'SETS',
    defaultPeriods: 3,
    defaultPeriodMinutes: 0,
    timerDirection: 'ELAPSED_ONLY',
    hasAddedTime: false,
    metrics: [
      { code: 'POINT', label: 'Punto Set', affectsScore: true, pointsValue: 1 },
      { code: 'FAULT', label: 'Falta de Malla/Línea', affectsScore: false },
    ],
  },
};

export const CATEGORY_PRESETS: Category[] = [
  { id: 'sub9', name: 'Sub-9', sport: 'FOOTBALL', periodDurationMinutes: 20, defaultPeriods: 2, description: 'Formato de desarrollo y técnica' },
  { id: 'sub13', name: 'Sub-13', sport: 'FOOTBALL', periodDurationMinutes: 30, defaultPeriods: 2, description: 'Competencia formativa' },
  { id: 'sub15', name: 'Sub-15', sport: 'FOOTBALL', periodDurationMinutes: 35, defaultPeriods: 2, description: 'Partidos competitivos' },
  { id: 'adult', name: 'Mayores', sport: 'FOOTBALL', periodDurationMinutes: 45, defaultPeriods: 2, description: 'Competencia profesional' },
  { id: 'futsal-elite', name: 'Futsal Elite', sport: 'FUTSAL', periodDurationMinutes: 20, defaultPeriods: 2, description: 'Partidos rápidos y intensos' },
  { id: 'basket-sub15', name: 'Sub-15', sport: 'BASKETBALL', periodDurationMinutes: 10, defaultPeriods: 4, description: 'Partidos juveniles' },
  { id: 'basket-adult', name: 'Mayores', sport: 'BASKETBALL', periodDurationMinutes: 12, defaultPeriods: 4, description: 'Partidos senior' },
];
