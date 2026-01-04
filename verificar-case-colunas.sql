-- Script para verificar o case exato das colunas no PostgreSQL
-- Execute este script no banco de dados

-- Verificar colunas da tabela routers (com case exato)
SELECT 
    column_name,
    data_type,
    CASE 
        WHEN column_name = LOWER(column_name) THEN 'lowercase'
        WHEN column_name = UPPER(column_name) THEN 'uppercase'
        ELSE 'mixed'
    END as case_type
FROM information_schema.columns
WHERE table_name = 'routers'
ORDER BY ordinal_position;

-- Verificar colunas da tabela tenants
SELECT 
    column_name,
    data_type,
    CASE 
        WHEN column_name = LOWER(column_name) THEN 'lowercase'
        WHEN column_name = UPPER(column_name) THEN 'uppercase'
        ELSE 'mixed'
    END as case_type
FROM information_schema.columns
WHERE table_name = 'tenants'
ORDER BY ordinal_position;

-- Testar queries com diferentes cases
-- Se funcionar:
SELECT id, name, tenant_id FROM routers LIMIT 1;

-- Se não funcionar, tente:
-- SELECT "Id", "Name", "TenantId" FROM routers LIMIT 1;
-- ou
-- SELECT "id", "name", "tenant_id" FROM routers LIMIT 1;

