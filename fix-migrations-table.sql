-- Script para corrigir a tabela __EFMigrationsHistory para usar snake_case
-- Execute este script no banco de dados antes de rodar as migrations

-- Verificar se a tabela existe e tem colunas em PascalCase
DO $$
BEGIN
    -- Renomear colunas se existirem em PascalCase
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' 
        AND column_name = 'MigrationId'
    ) THEN
        ALTER TABLE "__EFMigrationsHistory" RENAME COLUMN "MigrationId" TO "migration_id";
    END IF;
    
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' 
        AND column_name = 'ProductVersion'
    ) THEN
        ALTER TABLE "__EFMigrationsHistory" RENAME COLUMN "ProductVersion" TO "product_version";
    END IF;
END $$;

