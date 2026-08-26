import { useState, useEffect } from 'react';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

/**
 * Cascade Competition → Category → Team.
 *
 * Cada página con selects encadenados copiaba el mismo patrón de useState
 * y useApi. Este hook lo centraliza: el componente solo recibe y setea ids.
 *
 * @param {string} initialCompId
 * @param {string} initialCatId
 * @param {string} initialTeamId
 */
export function useCascade(initialCompId = '', initialCatId = '', initialTeamId = '') {
  const [compId, setCompId] = useState(initialCompId);
  const [catId, setCatId] = useState(initialCatId);
  const [teamId, setTeamId] = useState(initialTeamId);

  const { data: competiciones, isLoading: loadingComps } = useApi(endpoints.competitions);
  const { data: categorias, isLoading: loadingCats } = useApi(compId ? endpoints.categories(compId) : null);
  const { data: equipos, isLoading: loadingTeams } = useApi(catId ? endpoints.teams(catId) : null);

  // Reset downstream when parent changes
  useEffect(() => { setCatId(''); setTeamId(''); }, [compId]);
  useEffect(() => { setTeamId(''); }, [catId]);

  return {
    compId, setCompId,
    catId, setCatId,
    teamId, setTeamId,
    competiciones: competiciones || [],
    categorias: categorias || [],
    equipos: equipos || [],
    loadingComps,
    loadingCats,
    loadingTeams,
  };
}