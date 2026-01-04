-- Script para verificar o case das colunas no banco de dados
-- Execute este script para ver como as colunas estão realmente nomeadas

-- Verificar estrutura da tabela routers
SELECT 
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'routers'
ORDER BY ordinal_position;

-- Verificar estrutura da tabela tenants
SELECT 
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'tenants'
ORDER BY ordinal_position;

-- Testar query simples
SELECT id, name, tenant_id FROM routers LIMIT 1;

-- Se a query acima falhar, tente com aspas:
-- SELECT "id", "name", "tenant_id" FROM routers LIMIT 1;

