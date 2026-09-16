-- =============================================================================
-- AddSportEntrySize
--
-- Cuántos deportistas pueden formar una misma inscripción, dicho por el
-- deporte y no por quien carga la categoría.
--
-- sports.is_individual ya dice que se compite por persona y no por club, pero
-- no dice cuántas personas forman la unidad que compite. Esa diferencia es
-- real: en Kyorugi se pelea de a uno y una dupla no existe, mientras que
-- Poomsae corre individual, pareja y trío bajo el mismo is_individual (ver el
-- remark de IndividualTeamName, que ya documenta los tres casos).
--
-- Hasta ahora lo único que lo expresaba era categories.max_roster_size, y ese
-- campo nació para otra cosa: el plantel de un equipo. A quien carga una
-- categoría de taekwondo se le pregunta ahí "cuántos entran en la nómina", y
-- contesta pensando en cuántos deportistas va a anotar el club -- un nivel que
-- en su modelo mental ni existe. El resultado real, encontrado en datos de
-- prueba: una categoría de Kyorugi con max_roster_size = 50, que habilitaba
-- una "dupla" de cincuenta peleadores.
--
-- Null para un deporte de conjunto: ahí no hay techo propio del deporte, el
-- tamaño del plantel lo decide cada categoría. Mismo criterio que
-- default_minutes, que también queda null donde la pregunta no aplica.
-- =============================================================================

ALTER TABLE sports ADD COLUMN max_entry_size smallint;

COMMENT ON COLUMN sports.max_entry_size IS
  'Cuántos deportistas pueden formar una misma inscripción en este deporte.
   Null en un deporte de conjunto: el tamaño del plantel lo decide la
   categoría, no el deporte. El cupo efectivo de una categoría es el menor
   entre este techo y su propio max_roster_size.';

ALTER TABLE sports ADD CONSTRAINT ck_sports_max_entry_size_positive
    CHECK (max_entry_size IS NULL OR max_entry_size > 0);

-- Kyorugi es combate uno contra uno: la inscripción es siempre una persona.
UPDATE sports SET max_entry_size = 1 WHERE code = 'taekwondo_kyorugi';

-- Poomsae se presenta individual, en pareja o en trío, y cada modalidad es su
-- propia categoría; el techo del deporte es el trío.
UPDATE sports SET max_entry_size = 3 WHERE code = 'taekwondo_poomsae';
