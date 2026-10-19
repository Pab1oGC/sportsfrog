import { describe, expect, it } from 'vitest';
import { schoolModuleLinks } from 'src/pages/school-module-page';

describe('school module navigation', () => {
  it('keeps every school workflow connected to an existing dashboard route', () => {
    expect(schoolModuleLinks.map(({ path }) => path)).toEqual([
      '/dashboard/athletes',
      '/dashboard/categories',
      '/dashboard/teams',
      '/dashboard/competitions',
      '/dashboard/documents',
      '/dashboard/matches',
    ]);
  });
});
