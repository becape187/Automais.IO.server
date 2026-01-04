-- Script para marcar migrations antigas como aplicadas
-- Execute este script no banco de dados para evitar que o EF tente recriar tabelas existentes

-- Criar tabela de migrations se não existir (com PascalCase - padrão EF Core)
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "pk___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

-- Marcar todas as migrations existentes como aplicadas
-- Isso evita que o EF tente recriar tabelas que já existem no banco

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES 
    ('20251121010421_InitialCreate', '8.0.10')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES 
    ('20260103232909_router', '8.0.10')
ON CONFLICT ("MigrationId") DO NOTHING;

-- A migration wireguard será aplicada quando você executar update-database
-- Não precisa marcar aqui, pois ela ainda não foi aplicada

