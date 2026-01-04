-- Script para corrigir a tabela __EFMigrationsHistory para usar PascalCase
-- Execute este script no banco de dados ANTES de rodar novas migrations

-- Verificar estrutura atual
SELECT 
    column_name,
    data_type
FROM information_schema.columns
WHERE table_name = '__EFMigrationsHistory'
ORDER BY ordinal_position;

-- Renomear colunas de snake_case para PascalCase (se necessário)
DO $$
BEGIN
    -- Verificar se a coluna está em snake_case e renomear para PascalCase
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' 
        AND column_name = 'migration_id'
    ) THEN
        ALTER TABLE "__EFMigrationsHistory" 
        RENAME COLUMN "migration_id" TO "MigrationId";
        RAISE NOTICE 'Coluna migration_id renomeada para MigrationId';
    END IF;
    
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' 
        AND column_name = 'product_version'
    ) THEN
        ALTER TABLE "__EFMigrationsHistory" 
        RENAME COLUMN "product_version" TO "ProductVersion";
        RAISE NOTICE 'Coluna product_version renomeada para ProductVersion';
    END IF;
    
    -- Se as colunas já estão em PascalCase, não faz nada
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = '__EFMigrationsHistory' 
        AND column_name IN ('migration_id', 'product_version')
    ) THEN
        RAISE NOTICE 'Tabela __EFMigrationsHistory já está em PascalCase';
    END IF;
END $$;

-- Verificar estrutura final
SELECT 
    column_name,
    data_type
FROM information_schema.columns
WHERE table_name = '__EFMigrationsHistory'
ORDER BY ordinal_position;

