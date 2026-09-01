import { useApi } from 'src/hooks/use-api';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import { useRememberedChild } from 'src/hooks/use-remembered-child';
import { endpoints } from 'src/lib/axios';

/**
 * Cascade Competition → Category → Team.
 *
 * Cada página con selects encadenados copiaba el mismo patrón de useState
 * y useApi. Este hook lo centraliza: el componente solo recibe y setea ids.
 *
 * Ningún nivel arranca vacío si hay algo para mostrar. La competencia
 * recuerda la última usada en cualquier pantalla (o si es la primera vez,
 * elige la que está en curso) — ver useLastCompetition. La categoría y el
 * equipo hacen lo mismo un nivel más abajo, pero recordado *por padre*: la
 * categoría eleigida para el Torneo A no se le impone al Torneo B, así que
 * cambiar de competencia no arrastra la categoría de la anterior — resuelve
 * la que se recuerde para la nueva, o la primera si nunca se eligió una — ver
 * useRememberedChild.
 *
 * @param {string} initialCompId Un id que vino explicito de la URL, que le
 *   gana a lo recordado. Se ignora despues del primer render — cambiarlo
 *   una vez montado no vuelve a mover la seleccion.
 */
export function useCascade(initialCompId = '') {
  const { data: competiciones, isLoading: loadingComps } = useApi(endpoints.competitions);
  const [compId, setCompId] = useLastCompetition(competiciones, initialCompId);

  const { data: categorias, isLoading: loadingCats } = useApi(compId ? endpoints.categories(compId) : null);
  const [catId, setCatId] = useRememberedChild(categorias, compId, 'sportfrog:lastCategoryId:');

  const { data: equipos, isLoading: loadingTeams } = useApi(catId ? endpoints.teams(catId) : null);
  const [teamId, setTeamId] = useRememberedChild(equipos, catId, 'sportfrog:lastTeamId:');

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
