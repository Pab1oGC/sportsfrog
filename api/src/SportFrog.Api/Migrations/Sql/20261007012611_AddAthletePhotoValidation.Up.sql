-- =============================================================================
-- AddAthletePhotoValidation
--
-- Records what the photo validator said about each athlete's photograph.
--
-- The verdict lives on the athlete, not on the photograph, because the
-- photograph is already reduced to a key and the question a club asks is
-- "is this athlete's photo acceptable?", which is a fact about the athlete.
--
-- Existing athletes start as not_evaluated. That is the truth: none of their
-- photographs has ever been validated, so claiming any other state would be
-- inventing a verdict. Nothing here re-evaluates them; that is a separate job.
--
-- Reasons and warnings are kept as JSON lists of Spanish sentences, the same
-- text the validator returns. They are not personal data, but they describe a
-- minor's photograph, so they stay off every public view.
-- =============================================================================

CREATE TYPE photo_validation_state AS ENUM ('not_evaluated', 'approved', 'rejected');

ALTER TABLE athletes
    ADD COLUMN photo_validation_state    photo_validation_state NOT NULL DEFAULT 'not_evaluated',
    ADD COLUMN photo_validation_reasons  jsonb NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN photo_validation_warnings jsonb NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN photo_rules_version       text,
    ADD COLUMN photo_validated_at        timestamptz;

COMMENT ON COLUMN athletes.photo_validation_state IS
  'Verdict of the photo validator for the stored photograph. not_evaluated
   means the validator never answered for it, which is different from
   rejected: the photograph was not checked.';

COMMENT ON COLUMN athletes.photo_validation_reasons IS
  'Why the photograph was rejected, as a JSON list of sentences. Empty unless
   photo_validation_state is rejected.';

COMMENT ON COLUMN athletes.photo_validation_warnings IS
  'Rules the validator could not verify yet, as a JSON list. These never
   reject a photograph.';

COMMENT ON COLUMN athletes.photo_rules_version IS
  'Version of the validation rules that produced the verdict. Null when the
   photograph has not been evaluated.';

COMMENT ON COLUMN athletes.photo_validated_at IS
  'When the verdict was recorded. Null when the photograph has not been evaluated.';
