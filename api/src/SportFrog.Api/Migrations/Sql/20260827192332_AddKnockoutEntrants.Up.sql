-- =============================================================================
-- AddKnockoutEntrants
--
-- A category that plays groups first and a knockout after has two kinds of
-- team by the time the bracket is drawn: the ones who qualified and the ones
-- the group stage eliminated. Both still belong to the category — a losing
-- team is not deleted — so "every active team" stops being a usable answer
-- to "who is in the knockout" the moment groups exist. It already was the
-- answer for a category that goes straight to a knockout, and stays one:
-- this column is null there, and null means exactly what it always meant.
--
-- What it is for, specifically, is byes. A knockout entry list that is not a
-- power of two hands some teams a bye into round two without a match of
-- their own, and finding them again once round two is being drawn means
-- comparing "who has played a match this bracket" against "who entered it".
-- For a pure knockout that second set is every active team in the category;
-- for a category that ran groups first, it is this.
-- =============================================================================

ALTER TABLE categories ADD COLUMN knockout_entrants uuid[];

COMMENT ON COLUMN categories.knockout_entrants IS
  'Who qualified out of the group stage, in seeded order, the moment the
   knockout bracket was drawn. Null until then, and null forever for a
   category that was never played as groups. Not touched again afterwards —
   a bye is only ever a question about round one.';
