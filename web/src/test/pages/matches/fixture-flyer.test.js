import { describe, expect, it } from 'vitest';
import { FLYER_FORMATS, fixtureTitle, flyerFileName, flyerFormat, teamName } from 'src/pages/matches/fixture-flyer';

describe('fixture flyer data', () => {
  it('offers publication dimensions that match Instagram placements', () => {
    expect(FLYER_FORMATS).toEqual([
      expect.objectContaining({ value: 'story', width: 1080, height: 1920 }),
      expect.objectContaining({ value: 'portrait', width: 1080, height: 1350 }),
      expect.objectContaining({ value: 'square', width: 1080, height: 1080 }),
    ]);
  });

  it('uses the bracket placeholder when the team has not been decided', () => {
    const match = { homePlaceholder: 'Ganador de semifinal', awayTeamName: 'Club Atlético' };
    expect(teamName(match, 'home')).toBe('Ganador de semifinal');
    expect(fixtureTitle(match)).toBe('Ganador de semifinal vs Club Atlético');
  });

  it('creates a portable file name and safely falls back to the story format', () => {
    const match = { homeTeamName: 'Unión Ñuñoa', awayTeamName: 'Always Ready' };
    expect(flyerFileName(match, 'portrait')).toBe('fixture-union-nunoa-vs-always-ready-portrait.png');
    expect(flyerFileName(match, 'square', 'resultado')).toBe('resultado-union-nunoa-vs-always-ready-square.png');
    expect(flyerFormat('not-a-format')).toEqual(expect.objectContaining({ value: 'story' }));
  });
});
