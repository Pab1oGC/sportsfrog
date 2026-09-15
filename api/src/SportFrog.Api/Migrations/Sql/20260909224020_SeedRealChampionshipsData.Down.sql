-- =============================================================================
-- SeedRealChampionshipsData.Down.sql
--
-- Revierte la inserción de datos de prueba de Fútbol y Taekwondo.
-- =============================================================================

DELETE FROM competitions WHERE slug IN ('liga-profesional-2026', 'nacional-taekwondo-2026');
DELETE FROM rulesets WHERE name IN ('Reglamento Oficial FPF - Fútbol 11 (2026)', 'Reglamento Oficial WT - Kyorugi Best of 3');
DELETE FROM clubs WHERE name IN ('Club Bolívar', 'Club The Strongest', 'Club Jorge Wilstermann', 'Club Oriente Petrolero', 'Do-Jang Dragones Rojos TKD', 'Academia Cobra Martial Arts', 'Club Titanes del Altiplano');
DELETE FROM venues WHERE name IN ('Estadio Olímpico Hernando Siles', 'Coliseo Cerrado Julio Borelli Viteritto');
