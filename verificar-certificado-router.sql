-- Script para verificar se um router tem certificado WireGuard configurado
-- Substitua 'SEU_ROUTER_ID' pelo ID do router que você criou

-- Verificar router e seus peers
SELECT 
    r."Id" as router_id,
    r."Name" as router_name,
    r."VpnNetworkId" as tem_vpn_network_id,
    p."Id" as peer_id,
    p."PublicKey",
    p."AllowedIps" as router_ip_vpn,
    p."Endpoint" as servidor_endpoint,
    p."ListenPort",
    CASE 
        WHEN p."ConfigContent" IS NOT NULL AND LENGTH(p."ConfigContent") > 0 THEN '✅ Sim'
        ELSE '❌ Não'
    END as tem_certificado,
    LENGTH(p."ConfigContent") as tamanho_config_bytes,
    p."CreatedAt" as peer_criado_em
FROM routers r
LEFT JOIN router_wireguard_peers p ON p."RouterId" = r."Id"
WHERE r."Id" = 'SEU_ROUTER_ID_AQUI'  -- ⚠️ SUBSTITUA PELO ID DO SEU ROUTER
ORDER BY p."CreatedAt" DESC;

-- Ver redes permitidas do router
SELECT 
    r."Id" as router_id,
    r."Name" as router_name,
    n."NetworkCidr" as rede_permitida,
    n."Description",
    n."CreatedAt"
FROM routers r
INNER JOIN router_allowed_networks n ON n."RouterId" = r."Id"
WHERE r."Id" = 'SEU_ROUTER_ID_AQUI'  -- ⚠️ SUBSTITUA PELO ID DO SEU ROUTER
ORDER BY n."CreatedAt";

-- Ver conteúdo do certificado (se existir)
SELECT 
    "ConfigContent"
FROM router_wireguard_peers
WHERE "RouterId" = 'SEU_ROUTER_ID_AQUI'  -- ⚠️ SUBSTITUA PELO ID DO SEU ROUTER
LIMIT 1;

