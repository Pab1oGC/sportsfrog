-- =============================================================================
-- AddPerformanceRunningOrder
--
-- A classification stage pairs nobody, so it has no fixture the way a match
-- does — no opponent, no exact instant two sides agree to meet at. What an
-- organizer actually needs to say is where a team performs (which mat) and
-- its turn in that mat's running order for the day: poomsae routines run
-- barely a minute apart, one after another, so a clock time nobody would
-- keep to is no more honest than not having one at all — the same reasoning
-- that already keeps matches.uq_space_schedule to an exact instant instead
-- of a guessed duration.
--
-- venue_space_id mirrors matches.venue_space_id exactly (the same pitches
-- and mats are shared across every competition an organization runs).
-- scheduled_on + order_number replace matches.scheduled_at: a day, and a
-- position within that day's queue for that mat, rather than a time nobody
-- would actually start at.
-- =============================================================================

ALTER TABLE performances
    ADD COLUMN venue_space_id uuid REFERENCES venue_spaces(id),
    ADD COLUMN scheduled_on   date,
    ADD COLUMN order_number   smallint;

ALTER TABLE performances
    ADD CONSTRAINT ck_performance_order_positive CHECK (order_number IS NULL OR order_number > 0);

-- One performance per mat, per day, per turn — the running-order equivalent
-- of uq_space_schedule. Nothing here excludes an already-scored performance:
-- it really did occupy that turn, same as a finished match still occupies
-- the slot it was actually played in.
CREATE UNIQUE INDEX uq_performance_running_order
    ON performances(venue_space_id, scheduled_on, order_number)
    WHERE venue_space_id IS NOT NULL AND scheduled_on IS NOT NULL
        AND order_number IS NOT NULL AND deleted_at IS NULL;
