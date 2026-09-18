-- AddTaekwondoKyorugiScoringEvents — rollback

UPDATE sport_metrics
   SET affects_score = false
 WHERE sport_code = 'taekwondo_kyorugi' AND code = 'point';

UPDATE sport_metrics
   SET affects_score = false, counts_for_opponent = false
 WHERE sport_code = 'taekwondo_kyorugi' AND code = 'penalty';
