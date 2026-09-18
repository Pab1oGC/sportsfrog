-- AddPublicRecentResults — rollback

DROP FUNCTION IF EXISTS list_public_recent_results(int);
DROP POLICY IF EXISTS published_matches ON matches;
