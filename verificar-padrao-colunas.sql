-- Script para verificar o padrão de nomenclatura das colunas
-- Execute para ver se as colunas estão em PascalCase ou snake_case

-- Verificar colunas da tabela tenants (tabela antiga)
SELECT 
    column_name,
    data_type
FROM information_schema.columns
WHERE table_name = 'tenants'
ORDER BY ordinal_position;

-- Verificar colunas da tabela routers (tabela nova)
SELECT 
    column_name,
    data_type
FROM information_schema.columns
WHERE table_name = 'routers'
ORDER BY ordinal_position;

-- Comparar: se tenants tem "Id" e routers tem "id", há inconsistência

