-- Roles de aplicación deportiva
-- Ejecutados automáticamente por Postgres al montar el volumen

-- Role dueño de esquema (aplica migraciones)
CREATE ROLE sportfrog_owner WITH LOGIN PASSWORD 'cambiar_por_secreto';
GRANT CONNECT ON DATABASE sportfrog TO sportfrog_owner;
GRANT USAGE ON SCHEMA public TO sportfrog_owner;

-- Role aplicación (consumo de datos)
CREATE ROLE sportfrog_app WITH LOGIN PASSWORD 'cambiar_por_secreto';
GRANT CONNECT ON DATABASE sportfrog TO sportfrog_app;
GRANT USAGE ON SCHEMA public TO sportfrog_app;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO sportfrog_app;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO sportfrog_app;

-- Role público (lectura sola, vistas, logos, etc.)
CREATE ROLE sportfrog_public WITH LOGIN PASSWORD 'cambiar_por_secreto';
GRANT CONNECT ON DATABASE sportfrog TO sportfrog_public;
GRANT USAGE ON SCHEMA public TO sportfrog_public;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO sportfrog_public;

-- Otorgar permisos por defecto a futuras tablas
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT ON TABLES TO sportfrog_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT ON TABLES TO sportfrog_public;