# Lists

El lugar centralizado desde el que cualquier dato del sistema se exporta a
Excel o PDF — goleadores, posiciones, planteles, partidos — sin que la
exportación sea una característica aparte de cada feature que ya muestra esos
datos en pantalla. `Features/Reports/` y las pantallas de Líderes/Posiciones
siguen siendo la lectura correcta de esos datos; esto es a dónde venir cuando
lo que hace falta es llevárselos.

## La idea

Una lista es tres cosas: un `slug` estable, los parámetros que necesita
nombrados por `ListParameterKind` (no por su nombre — eso es lo que deja que
`GET /lists` arme un catálogo genérico y que la página web construya el
formulario de filtros sin conocer de antemano qué lista eligieron), y una
función que las carga como un `ListTable`. Ni ella ni los dos renderers saben
nada la una de la otra: `ListRegistry` resuelve una por su slug, y
`ListXlsx`/`ListPdf` dibujan cualquier `ListTable` que les llegue sin
preguntar quién la hizo.

## Agregar una lista nueva

1. Implementar `IListProvider` en `Providers/` (nombre: `AlgoList`). Devolvé
   `null` de `LoadAsync` solo cuando el alcance no nombra algo real — una
   categoría/equipo con datos pero sin nada interesante que decir (una
   categoría sin tarjetas, una final no jugada) es un `ListTable` con
   `Sections` vacío y, si hace falta explicar por qué, un `Subtitle` — no un
   `null`. Mirá `CardsList` o `RosterByPositionList` para el patrón.
2. Registrarlo en `Program.cs`, junto a los demás:
   `builder.Services.AddSingleton<IListProvider, AlgoList>();` — `Singleton`
   mientras el provider no dependa de nada *scoped* (`ObjectStore`,
   `AthletePhoto`...). El día que uno lo necesite, el contenedor rechaza el
   arranque en voz alta, y ese es el momento de pasar esa única línea a
   `AddScoped`, no antes.
3. Agregarlo a `ListProvidersParityTests.Slugs()`/`ResolveCase` (API) y, si
   introduce un `ListParameterKind` que la página web todavía no sabe
   renderizar, a `neededCascadeLevels`/`scopeFromCascade` en
   `web/src/pages/lists/list-scope.js`.

Eso alcanza. El catálogo (`GET /lists`), la vista previa, los dos exports y el
formulario de filtros de la página web no se tocan — todos leen el contrato
genérico (`IListProvider.Parameters`, `ListTable`), no una lista a la vez.

## Lo compartido, para no duplicarlo

- **`ListCellValues`** interpreta un valor crudo según su `ListValueKind`
  (`ToNumber`, `ToDisplayText`...). Es la única razón por la que Excel y PDF
  nunca pueden decir dos cosas distintas del mismo dato: los dos leen una
  celda a través de acá, no cada uno a su manera.
- **`TeamRosterRows`** es el plantel de un equipo, compartido por `RosterList`
  y `RosterByPositionList`.
- **`LeaderBoardSections`** es un tablero de `LeadersQuery` vuelto filas,
  compartido por `LeadersList` y `CardsList`.
- **`ChampionResolver`** responde "quién salió N° en esta categoría" —
  liga/grupos por tabla, llave por su final (vía `MatchWinner`, que ya
  entiende walkover y penales), juzgada por `ClassificationRanking`. No
  resuelve un tercer puesto de llave: ese depende de un desempate de bronce
  que este resolver no intenta.

## La red de seguridad

`ListProvidersParityTests` corre cada provider realmente registrado contra un
alcance que puede resolver, y renderiza el resultado en los dos formatos.
Agregá el nuevo slug a `Slugs()`/`ResolveCase()` (paso 3 arriba) y la lista
queda cubierta exactamente igual que las demás — es la prueba que atrapa una
columna cuyo `ListValueKind` declarado no coincide con el tipo real de sus
valores, algo que ningún test de un solo renderer, armado con una tabla
elegida a mano, fabricaría nunca.
