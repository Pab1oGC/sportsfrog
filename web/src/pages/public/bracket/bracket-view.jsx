import Alert from '@mui/material/Alert';
import { agruparPorRonda, soloFaseEliminatoria } from 'src/lib/match-rounds';
import { SectionSkeleton } from 'src/components/section-skeleton';
import { Llave } from './llave';
import { RepechageBracket } from './repechage-bracket';

/**
 * La pestaña "Llave" -- de primer nivel, no un sub-modo escondido dentro
 * de Calendario. Comparte el mismo `data` (fixtures) que ya trae
 * Calendario, así que no hay pedido nuevo a la API: `public-competition.jsx`
 * pide `/matches` en cuanto cualquiera de las dos pestañas está activa.
 *
 * La pestaña existe (ver comp.shows.bracket en ReadPublicCompetition) en
 * cuanto la COMPETENCIA tiene al menos un partido de fase eliminatoria en
 * cualquier categoría -- pero la categoría que el visitante tenga
 * elegida puede no ser esa. Acá se resuelve ese caso con un estado vacío,
 * no escondiendo la pestaña: mismo criterio que ya usa StandingsView con
 * "Sin datos de posiciones".
 *
 * El repechaje de kyorugi (`isRepechage`) se separa acá, antes de
 * `agruparPorRonda`, y se dibuja aparte con `RepechageBracket` -- nunca entra
 * a `Llave`. Sus partidos también llevan `phase` (ver
 * `SportFrog.Domain.Scheduling.Repechage` del lado del servidor), y
 * mezclarlos en la misma llave le daría a sus columnas y a
 * `BracketConnectors` una forma que ninguna de las dos mitades del repechaje
 * en realidad tiene.
 */
export function BracketView(props) {
  var loading = props.loading;
  var selectedCatId = props.selectedCatId;

  if (loading) return <SectionSkeleton kind="bracket" />;

  var fixtures = (props.data && props.data.fixtures) || [];
  if (selectedCatId) fixtures = fixtures.filter(function(m) { return m.categoryId === selectedCatId; });

  var repechaje = fixtures.filter(function(m) { return m.isRepechage; });
  var delCuadro = fixtures.filter(function(m) { return !m.isRepechage; });

  var gruposFase = soloFaseEliminatoria(agruparPorRonda(delCuadro));

  if (gruposFase.length === 0 && repechaje.length === 0) {
    return (
      <Alert severity="info">
        Todavía no hay partidos de eliminatoria para esta categoría.
      </Alert>
    );
  }

  return (
    <>
      {gruposFase.length > 0 && (
        <Llave grupos={gruposFase} variant={props.bracketVariant} esIndividual={props.esIndividual} />
      )}
      {repechaje.length > 0 && (
        <RepechageBracket matches={repechaje} variant={props.bracketVariant} esIndividual={props.esIndividual} />
      )}
    </>
  );
}
