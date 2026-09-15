/**
 * T-19 — Contexto global de tematización por campeonato.
 *
 * Cada competencia posee su propia identidad visual (colores, banner, sponsors).
 * El tema se consulta prioritariamente por ID/slug de competencia.
 * Si un campeonato no tiene un tema personalizado explícito, se asigna
 * una paleta y banner profesional por defecto según su disciplina deportiva.
 */

import { createContext, useContext, useState, useCallback, useEffect } from 'react';

const STORAGE_KEY = 'sf_tournament_theme';

export const DEFAULT_THEME = {
  primaryColor: '#1B8A2E',
  secondaryColor: '#0D4E1A',
  accentColor: '#F5A623',
  bannerUrl: null,
  logoUrl: null,
  sponsors: [],
  competitionName: '',
  competitionId: null,
  competitionSlug: null,
};

export const SPORT_DEFAULT_THEMES = {
  football: {
    primaryColor: '#1B8A2E',
    secondaryColor: '#0D4E1A',
    accentColor: '#F5A623',
    bannerUrl: 'https://images.unsplash.com/photo-1508098682722-e99c43a406b2?auto=format&fit=crop&w=1200&q=80',
  },
  futsal: {
    primaryColor: '#059669',
    secondaryColor: '#064E3B',
    accentColor: '#34D399',
    bannerUrl: 'https://images.unsplash.com/photo-1574629810360-7efbbe195018?auto=format&fit=crop&w=1200&q=80',
  },
  taekwondo_kyorugi: {
    primaryColor: '#991B1B',
    secondaryColor: '#1E3A8A',
    accentColor: '#DB2777',
    bannerUrl: 'https://images.unsplash.com/photo-1555597673-b21d5c935865?auto=format&fit=crop&w=1200&q=80',
  },
  taekwondo_poomsae: {
    primaryColor: '#1E3A8A',
    secondaryColor: '#991B1B',
    accentColor: '#F59E0B',
    bannerUrl: 'https://images.unsplash.com/photo-1555597673-b21d5c935865?auto=format&fit=crop&w=1200&q=80',
  },
  basketball: {
    primaryColor: '#C2410C',
    secondaryColor: '#7C2D12',
    accentColor: '#F97316',
    bannerUrl: 'https://images.unsplash.com/photo-1546519638-68e109498ffc?auto=format&fit=crop&w=1200&q=80',
  },
  volleyball: {
    primaryColor: '#0284C7',
    secondaryColor: '#075985',
    accentColor: '#38BDF8',
    bannerUrl: 'https://images.unsplash.com/photo-1612872087720-bb876e2e67d1?auto=format&fit=crop&w=1200&q=80',
  },
};

function loadFromStorage() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? { ...DEFAULT_THEME, ...JSON.parse(raw) } : DEFAULT_THEME;
  } catch {
    return DEFAULT_THEME;
  }
}

function saveToStorage(theme) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(theme));
    if (theme.competitionId) {
      localStorage.setItem(`sf_theme_${theme.competitionId}`, JSON.stringify(theme));
    }
    if (theme.competitionSlug) {
      localStorage.setItem(`sf_theme_${theme.competitionSlug}`, JSON.stringify(theme));
    }
  } catch { /* modo privado */ }
}

export function getThemeForCompetition(comp) {
  if (!comp) return DEFAULT_THEME;

  const sportCode = comp.sportCode || comp.sport_code || 'football';
  const sportDefaults = SPORT_DEFAULT_THEMES[sportCode] || SPORT_DEFAULT_THEMES.football;

  try {
    if (comp.id) {
      const rawId = localStorage.getItem(`sf_theme_${comp.id}`);
      if (rawId) {
        const parsed = JSON.parse(rawId);
        return { ...DEFAULT_THEME, ...sportDefaults, ...parsed };
      }
    }
    if (comp.slug) {
      const rawSlug = localStorage.getItem(`sf_theme_${comp.slug}`);
      if (rawSlug) {
        const parsed = JSON.parse(rawSlug);
        return { ...DEFAULT_THEME, ...sportDefaults, ...parsed };
      }
    }
    const rawGlobal = localStorage.getItem(STORAGE_KEY);
    if (rawGlobal) {
      const parsedGlobal = JSON.parse(rawGlobal);
      // Únicamente usar la preferencia global si coincide explícitamente con este campeonato
      if (
        (parsedGlobal.competitionId && parsedGlobal.competitionId === comp.id) ||
        (parsedGlobal.competitionSlug && parsedGlobal.competitionSlug === comp.slug)
      ) {
        return { ...DEFAULT_THEME, ...sportDefaults, ...parsedGlobal };
      }
    }
  } catch { /* ignore */ }

  const pub = comp.settings?.public || comp.portal || {};
  const preview = comp.publicPreview || {};

  return {
    primaryColor: pub.accentColor || sportDefaults.primaryColor,
    secondaryColor: sportDefaults.secondaryColor,
    accentColor: pub.accentColor || sportDefaults.accentColor,
    bannerUrl: pub.bannerUrl || preview.bannerUrl || sportDefaults.bannerUrl,
    logoUrl: comp.organizationLogoUrl || null,
    sponsors: pub.sponsors || [],
    competitionName: comp.name || '',
    competitionId: comp.id || null,
    competitionSlug: comp.slug || null,
  };
}

const TournamentThemeContext = createContext({
  theme: DEFAULT_THEME,
  setTheme: () => {},
  applyTheme: () => {},
  resetTheme: () => {},
});

export function TournamentThemeProvider({ children }) {
  const [theme, setThemeState] = useState(loadFromStorage);

  // Inyecta CSS custom properties en :root cada vez que el tema cambia.
  useEffect(function () {
    const root = document.documentElement;
    root.style.setProperty('--sf-primary', theme.primaryColor);
    root.style.setProperty('--sf-secondary', theme.secondaryColor);
    root.style.setProperty('--sf-accent', theme.accentColor);
    if (theme.bannerUrl) {
      root.style.setProperty('--sf-banner-url', `url("${theme.bannerUrl}")`);
    } else {
      root.style.removeProperty('--sf-banner-url');
    }
  }, [theme]);

  const setTheme = useCallback(function (patch) {
    setThemeState(function (prev) {
      const next = { ...prev, ...patch };
      saveToStorage(next);
      return next;
    });
  }, []);

  // Recibe un objeto competition del API y extrae su tema
  const applyTheme = useCallback(function (competition) {
    if (!competition) return;
    const computed = getThemeForCompetition(competition);
    const next = {
      ...computed,
      competitionName: competition.name || computed.competitionName,
      competitionId: competition.id || computed.competitionId,
      competitionSlug: competition.slug || computed.competitionSlug,
    };
    setThemeState(next);
    saveToStorage(next);
  }, []);

  const resetTheme = useCallback(function () {
    setThemeState(DEFAULT_THEME);
    saveToStorage(DEFAULT_THEME);
  }, []);

  return (
    <TournamentThemeContext value={{ theme, setTheme, applyTheme, resetTheme }}>
      {children}
    </TournamentThemeContext>
  );
}

export function useTournamentTheme() {
  return useContext(TournamentThemeContext);
}
