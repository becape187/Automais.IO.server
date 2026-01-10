-- Script para remover a tabela vpn_servers e a coluna VpnServerId de vpn_networks
-- Execute este script no banco de dados PostgreSQL

-- 1. Remover a foreign key constraint
ALTER TABLE public.vpn_networks 
    DROP CONSTRAINT IF EXISTS FK_vpn_networks_vpn_servers_VpnServerId;

-- 2. Remover o índice
DROP INDEX IF EXISTS public.IX_vpn_networks_VpnServerId;

-- 3. Remover a coluna VpnServerId da tabela vpn_networks
ALTER TABLE public.vpn_networks 
    DROP COLUMN IF EXISTS "VpnServerId";

-- 4. Remover a tabela vpn_servers
DROP TABLE IF EXISTS public.vpn_servers CASCADE;

-- 5. Verificar se foi removido
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'vpn_servers') THEN
        RAISE NOTICE 'AVISO: A tabela vpn_servers ainda existe!';
    ELSE
        RAISE NOTICE 'SUCESSO: A tabela vpn_servers foi removida.';
    END IF;
    
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'vpn_networks' AND column_name = 'VpnServerId') THEN
        RAISE NOTICE 'AVISO: A coluna VpnServerId ainda existe em vpn_networks!';
    ELSE
        RAISE NOTICE 'SUCESSO: A coluna VpnServerId foi removida de vpn_networks.';
    END IF;
END $$;

