-- Script para atualizar endpoints WireGuard de srv01.automais.io para automais.io
-- Execute este script no banco de dados para corrigir routers criados antes da correção

-- Verificar quantos registros serão afetados
SELECT 
    COUNT(*) as total_afetados,
    COUNT(CASE WHEN endpoint = 'srv01.automais.io' THEN 1 END) as com_srv01,
    COUNT(CASE WHEN endpoint = 'automais.io' THEN 1 END) as com_automais_io
FROM router_wireguard_peers
WHERE endpoint IS NOT NULL;

-- Atualizar endpoints de srv01.automais.io para automais.io
UPDATE router_wireguard_peers
SET 
    endpoint = 'automais.io',
    updated_at = NOW()
WHERE endpoint = 'srv01.automais.io';

-- Verificar resultado
SELECT 
    id,
    router_id,
    vpn_network_id,
    endpoint,
    listen_port,
    created_at,
    updated_at
FROM router_wireguard_peers
WHERE endpoint IS NOT NULL
ORDER BY updated_at DESC;

