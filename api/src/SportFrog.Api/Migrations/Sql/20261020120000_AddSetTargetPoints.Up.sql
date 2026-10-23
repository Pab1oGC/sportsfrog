-- =============================================================================
-- AddSetTargetPoints
--
-- SetsResultShape has, until now, only checked the shape of a set-by-set
-- result: no period tied, the winner takes exactly the periods needed to
-- win, the loser does not also reach that count. None of that reads the
-- points inside a period at all -- a volleyball set reported 20-18 passes
-- every one of those checks exactly as well as a real 25-23 does, because
-- nothing told it 20-18 was never a legal way for a set to end.
--
-- target_points_per_set is that number, read where the sport actually has
-- one: a set in volleyball is a race to 25 points, won by whoever gets
-- there with at least a two-point lead (SetsResultShape is where that
-- margin itself lives, as a constant -- every sport this column will ever
-- describe shares the same "win by two" shape, so it is not a second
-- column here). target_points_deciding_set covers the one period that
-- races to a different number: volleyball's fifth set, decided at 15
-- rather than 25.
--
-- Left null for every sport that is not volleyball. A taekwondo asalto is
-- also score_mode = 'sets', but it is not a race to a point total at all --
-- it runs to the end of its clock and whichever side has more when that
-- happens wins it, so there is no target to state. wally shares
-- volleyball's shape in the catalog, but not a confirmed rule of its own,
-- so it is left null rather than guessed at.
-- =============================================================================

ALTER TABLE sports
    ADD COLUMN target_points_per_set smallint,
    ADD COLUMN target_points_deciding_set smallint;

UPDATE sports
   SET target_points_per_set = 25, target_points_deciding_set = 15
 WHERE code = 'volleyball';
