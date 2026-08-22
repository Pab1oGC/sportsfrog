export type SportCode = 'FOOTBALL' | 'FUTSAL' | 'BASKETBALL' | 'VOLLEYBALL' | 'WALLY';
export type ScoreMode = 'GOALS' | 'POINTS' | 'SETS';
export type TimerDirection = 'COUNT_UP' | 'COUNT_DOWN' | 'ELAPSED_ONLY';

export interface SportMetric {
  code: string;
  label: string;
  affectsScore: boolean;
  pointsValue?: number;
}

export interface SportRuleConfig {
  code: SportCode;
  name: string;
  scoreMode: ScoreMode;
  defaultPeriods: number;
  defaultPeriodMinutes: number;
  timerDirection: TimerDirection;
  hasAddedTime: boolean;
  metrics: SportMetric[];
}

export interface Category {
  id: string;
  name: string;
  sport: SportCode;
  periodDurationMinutes: number;
  defaultPeriods: number;
  description: string;
}

export interface VenueSpace {
  id: number;
  venueId: number;
  name: string;
  surface: 'Césped natural' | 'Sintético' | 'Parquet';
  capacity: number;
  available: boolean;
}
