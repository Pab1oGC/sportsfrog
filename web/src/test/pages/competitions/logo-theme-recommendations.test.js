import { describe, expect, it } from 'vitest';
import { contrastRatio, isHex } from 'src/lib/portal-theme';
import { buildThemeRecommendations } from 'src/pages/competitions/logo-theme-recommendations';

describe('recomendaciones visuales desde el logo', () => {
  it('arma tres direcciones completas y válidas con los colores de marca', () => {
    const recommendations = buildThemeRecommendations(['#087A45', '#F5C400', '#FFFFFF']);

    expect(recommendations.map((item) => item.id)).toEqual(['identity', 'sport', 'premium']);
    expect(recommendations[0].theme.primary).toBe('#087A45');
    expect(recommendations[0].theme.secondary).toBe('#F5C400');

    recommendations.forEach(({ theme }) => {
      ['primary', 'primaryContrast', 'secondary', 'surface', 'heroGradientTo', 'contentFigureColor']
        .forEach((field) => expect(isHex(theme[field])).toBe(true));
      expect(contrastRatio(theme.primary, theme.primaryContrast)).toBeGreaterThanOrEqual(4.5);
      expect(theme.heroStyle).toBe('gradient');
      expect(theme.headingFont).toBeTruthy();
      expect(theme.heroVariant).toBeTruthy();
    });
  });

  it('produce propuestas útiles aun si el escudo es monocromático', () => {
    const recommendations = buildThemeRecommendations(['#222222']);

    expect(recommendations).toHaveLength(3);
    expect(recommendations[0].theme.secondary).not.toBe(recommendations[0].theme.primary);
    expect(recommendations[1].theme.heroVariant).toBe('scoreboard');
    expect(recommendations[2].theme.heroVariant).toBe('editorial');
  });

  it('usa una paleta segura si no recibe colores analizables', () => {
    const recommendations = buildThemeRecommendations([]);

    expect(recommendations[0].theme.primary).toBe('#1769AA');
    expect(recommendations[0].theme.secondary).toBe('#F4B400');
  });
});
