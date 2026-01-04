using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class wireguard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_applications_tenants_tenant_id",
                table: "applications");

            migrationBuilder.DropForeignKey(
                name: "fk_devices_applications_application_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "fk_devices_tenants_tenant_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "fk_devices_vpn_networks_vpn_network_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "fk_gateways_tenants_tenant_id",
                table: "gateways");

            migrationBuilder.DropForeignKey(
                name: "fk_router_backups_routers_router_id",
                table: "router_backups");

            migrationBuilder.DropForeignKey(
                name: "fk_router_backups_tenant_users_created_by_user_id",
                table: "router_backups");

            migrationBuilder.DropForeignKey(
                name: "fk_router_backups_tenants_tenant_id",
                table: "router_backups");

            migrationBuilder.DropForeignKey(
                name: "fk_router_config_logs_routers_router_id",
                table: "router_config_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_router_config_logs_tenant_users_portal_user_id",
                table: "router_config_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_router_config_logs_tenants_tenant_id",
                table: "router_config_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_router_wireguard_peers_routers_router_id",
                table: "router_wireguard_peers");

            migrationBuilder.DropForeignKey(
                name: "fk_router_wireguard_peers_vpn_networks_vpn_network_id",
                table: "router_wireguard_peers");

            migrationBuilder.DropForeignKey(
                name: "fk_routers_tenants_tenant_id",
                table: "routers");

            migrationBuilder.DropForeignKey(
                name: "fk_routers_vpn_networks_vpn_network_id",
                table: "routers");

            migrationBuilder.DropForeignKey(
                name: "fk_tenant_users_tenants_tenant_id",
                table: "tenant_users");

            migrationBuilder.DropForeignKey(
                name: "fk_vpn_network_memberships_tenant_users_tenant_user_id",
                table: "vpn_network_memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_vpn_network_memberships_vpn_networks_vpn_network_id",
                table: "vpn_network_memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_vpn_networks_tenants_tenant_id",
                table: "vpn_networks");

            migrationBuilder.DropPrimaryKey(
                name: "pk_vpn_networks",
                table: "vpn_networks");

            migrationBuilder.DropPrimaryKey(
                name: "pk_vpn_network_memberships",
                table: "vpn_network_memberships");

            migrationBuilder.DropPrimaryKey(
                name: "pk_tenants",
                table: "tenants");

            migrationBuilder.DropPrimaryKey(
                name: "pk_tenant_users",
                table: "tenant_users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_routers",
                table: "routers");

            migrationBuilder.DropPrimaryKey(
                name: "pk_router_wireguard_peers",
                table: "router_wireguard_peers");

            migrationBuilder.DropPrimaryKey(
                name: "pk_router_config_logs",
                table: "router_config_logs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_router_backups",
                table: "router_backups");

            migrationBuilder.DropPrimaryKey(
                name: "pk_gateways",
                table: "gateways");

            migrationBuilder.DropPrimaryKey(
                name: "pk_devices",
                table: "devices");

            migrationBuilder.DropPrimaryKey(
                name: "pk_applications",
                table: "applications");

            migrationBuilder.RenameColumn(
                name: "slug",
                table: "vpn_networks",
                newName: "Slug");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "vpn_networks",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "vpn_networks",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "cidr",
                table: "vpn_networks",
                newName: "Cidr");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "vpn_networks",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "vpn_networks",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "vpn_networks",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "is_default",
                table: "vpn_networks",
                newName: "IsDefault");

            migrationBuilder.RenameColumn(
                name: "dns_servers",
                table: "vpn_networks",
                newName: "DnsServers");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "vpn_networks",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_vpn_networks_tenant_id_slug",
                table: "vpn_networks",
                newName: "IX_vpn_networks_TenantId_Slug");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "vpn_network_memberships",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "vpn_network_id",
                table: "vpn_network_memberships",
                newName: "VpnNetworkId");

            migrationBuilder.RenameColumn(
                name: "tenant_user_id",
                table: "vpn_network_memberships",
                newName: "TenantUserId");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "vpn_network_memberships",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "vpn_network_memberships",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "assigned_ip",
                table: "vpn_network_memberships",
                newName: "AssignedIp");

            migrationBuilder.RenameIndex(
                name: "ix_vpn_network_memberships_vpn_network_id_tenant_user_id",
                table: "vpn_network_memberships",
                newName: "IX_vpn_network_memberships_VpnNetworkId_TenantUserId");

            migrationBuilder.RenameIndex(
                name: "ix_vpn_network_memberships_tenant_user_id",
                table: "vpn_network_memberships",
                newName: "IX_vpn_network_memberships_TenantUserId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "tenants",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "slug",
                table: "tenants",
                newName: "Slug");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "tenants",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "metadata",
                table: "tenants",
                newName: "Metadata");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "tenants",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "tenants",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "tenants",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "chirp_stack_tenant_id",
                table: "tenants",
                newName: "ChirpStackTenantId");

            migrationBuilder.RenameIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                newName: "IX_tenants_Slug");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "tenant_users",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "tenant_users",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "tenant_users",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "tenant_users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "tenant_users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "vpn_public_key",
                table: "tenant_users",
                newName: "VpnPublicKey");

            migrationBuilder.RenameColumn(
                name: "vpn_ip_address",
                table: "tenant_users",
                newName: "VpnIpAddress");

            migrationBuilder.RenameColumn(
                name: "vpn_enabled",
                table: "tenant_users",
                newName: "VpnEnabled");

            migrationBuilder.RenameColumn(
                name: "vpn_device_name",
                table: "tenant_users",
                newName: "VpnDeviceName");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "tenant_users",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "tenant_users",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "last_login_at",
                table: "tenant_users",
                newName: "LastLoginAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "tenant_users",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_tenant_users_tenant_id_email",
                table: "tenant_users",
                newName: "IX_tenant_users_TenantId_Email");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "routers",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "routers",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "model",
                table: "routers",
                newName: "Model");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "routers",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "routers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "vpn_network_id",
                table: "routers",
                newName: "VpnNetworkId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "routers",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "routers",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "serial_number",
                table: "routers",
                newName: "SerialNumber");

            migrationBuilder.RenameColumn(
                name: "router_os_api_username",
                table: "routers",
                newName: "RouterOsApiUsername");

            migrationBuilder.RenameColumn(
                name: "router_os_api_url",
                table: "routers",
                newName: "RouterOsApiUrl");

            migrationBuilder.RenameColumn(
                name: "router_os_api_password",
                table: "routers",
                newName: "RouterOsApiPassword");

            migrationBuilder.RenameColumn(
                name: "last_seen_at",
                table: "routers",
                newName: "LastSeenAt");

            migrationBuilder.RenameColumn(
                name: "hardware_info",
                table: "routers",
                newName: "HardwareInfo");

            migrationBuilder.RenameColumn(
                name: "firmware_version",
                table: "routers",
                newName: "FirmwareVersion");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "routers",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_routers_vpn_network_id",
                table: "routers",
                newName: "IX_routers_VpnNetworkId");

            migrationBuilder.RenameIndex(
                name: "ix_routers_tenant_id",
                table: "routers",
                newName: "IX_routers_TenantId");

            migrationBuilder.RenameIndex(
                name: "ix_routers_serial_number",
                table: "routers",
                newName: "IX_routers_SerialNumber");

            migrationBuilder.RenameColumn(
                name: "endpoint",
                table: "router_wireguard_peers",
                newName: "Endpoint");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "router_wireguard_peers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "vpn_network_id",
                table: "router_wireguard_peers",
                newName: "VpnNetworkId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "router_wireguard_peers",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "router_id",
                table: "router_wireguard_peers",
                newName: "RouterId");

            migrationBuilder.RenameColumn(
                name: "public_key",
                table: "router_wireguard_peers",
                newName: "PublicKey");

            migrationBuilder.RenameColumn(
                name: "private_key",
                table: "router_wireguard_peers",
                newName: "PrivateKey");

            migrationBuilder.RenameColumn(
                name: "listen_port",
                table: "router_wireguard_peers",
                newName: "ListenPort");

            migrationBuilder.RenameColumn(
                name: "last_handshake",
                table: "router_wireguard_peers",
                newName: "LastHandshake");

            migrationBuilder.RenameColumn(
                name: "is_enabled",
                table: "router_wireguard_peers",
                newName: "IsEnabled");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "router_wireguard_peers",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "bytes_sent",
                table: "router_wireguard_peers",
                newName: "BytesSent");

            migrationBuilder.RenameColumn(
                name: "bytes_received",
                table: "router_wireguard_peers",
                newName: "BytesReceived");

            migrationBuilder.RenameColumn(
                name: "allowed_ips",
                table: "router_wireguard_peers",
                newName: "AllowedIps");

            migrationBuilder.RenameIndex(
                name: "ix_router_wireguard_peers_vpn_network_id",
                table: "router_wireguard_peers",
                newName: "IX_router_wireguard_peers_VpnNetworkId");

            migrationBuilder.RenameIndex(
                name: "ix_router_wireguard_peers_router_id_vpn_network_id",
                table: "router_wireguard_peers",
                newName: "IX_router_wireguard_peers_RouterId_VpnNetworkId");

            migrationBuilder.RenameIndex(
                name: "ix_router_wireguard_peers_router_id",
                table: "router_wireguard_peers",
                newName: "IX_router_wireguard_peers_RouterId");

            migrationBuilder.RenameColumn(
                name: "timestamp",
                table: "router_config_logs",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "source",
                table: "router_config_logs",
                newName: "Source");

            migrationBuilder.RenameColumn(
                name: "details",
                table: "router_config_logs",
                newName: "Details");

            migrationBuilder.RenameColumn(
                name: "category",
                table: "router_config_logs",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "action",
                table: "router_config_logs",
                newName: "Action");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "router_config_logs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "router_config_logs",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "router_os_user",
                table: "router_config_logs",
                newName: "RouterOsUser");

            migrationBuilder.RenameColumn(
                name: "router_id",
                table: "router_config_logs",
                newName: "RouterId");

            migrationBuilder.RenameColumn(
                name: "portal_user_id",
                table: "router_config_logs",
                newName: "PortalUserId");

            migrationBuilder.RenameColumn(
                name: "config_path",
                table: "router_config_logs",
                newName: "ConfigPath");

            migrationBuilder.RenameColumn(
                name: "before_value",
                table: "router_config_logs",
                newName: "BeforeValue");

            migrationBuilder.RenameColumn(
                name: "after_value",
                table: "router_config_logs",
                newName: "AfterValue");

            migrationBuilder.RenameIndex(
                name: "ix_router_config_logs_timestamp",
                table: "router_config_logs",
                newName: "IX_router_config_logs_Timestamp");

            migrationBuilder.RenameIndex(
                name: "ix_router_config_logs_tenant_id",
                table: "router_config_logs",
                newName: "IX_router_config_logs_TenantId");

            migrationBuilder.RenameIndex(
                name: "ix_router_config_logs_router_id",
                table: "router_config_logs",
                newName: "IX_router_config_logs_RouterId");

            migrationBuilder.RenameIndex(
                name: "ix_router_config_logs_portal_user_id",
                table: "router_config_logs",
                newName: "IX_router_config_logs_PortalUserId");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "router_backups",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "router_backups",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "router_backups",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "router_os_version",
                table: "router_backups",
                newName: "RouterOsVersion");

            migrationBuilder.RenameColumn(
                name: "router_model",
                table: "router_backups",
                newName: "RouterModel");

            migrationBuilder.RenameColumn(
                name: "router_id",
                table: "router_backups",
                newName: "RouterId");

            migrationBuilder.RenameColumn(
                name: "is_automatic",
                table: "router_backups",
                newName: "IsAutomatic");

            migrationBuilder.RenameColumn(
                name: "file_size_bytes",
                table: "router_backups",
                newName: "FileSizeBytes");

            migrationBuilder.RenameColumn(
                name: "file_path",
                table: "router_backups",
                newName: "FilePath");

            migrationBuilder.RenameColumn(
                name: "file_name",
                table: "router_backups",
                newName: "FileName");

            migrationBuilder.RenameColumn(
                name: "file_hash",
                table: "router_backups",
                newName: "FileHash");

            migrationBuilder.RenameColumn(
                name: "created_by_user_id",
                table: "router_backups",
                newName: "CreatedByUserId");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "router_backups",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "command_count",
                table: "router_backups",
                newName: "CommandCount");

            migrationBuilder.RenameIndex(
                name: "ix_router_backups_tenant_id",
                table: "router_backups",
                newName: "IX_router_backups_TenantId");

            migrationBuilder.RenameIndex(
                name: "ix_router_backups_router_id",
                table: "router_backups",
                newName: "IX_router_backups_RouterId");

            migrationBuilder.RenameIndex(
                name: "ix_router_backups_created_by_user_id",
                table: "router_backups",
                newName: "IX_router_backups_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "ix_router_backups_created_at",
                table: "router_backups",
                newName: "IX_router_backups_CreatedAt");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "gateways",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "gateways",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "metadata",
                table: "gateways",
                newName: "Metadata");

            migrationBuilder.RenameColumn(
                name: "longitude",
                table: "gateways",
                newName: "Longitude");

            migrationBuilder.RenameColumn(
                name: "latitude",
                table: "gateways",
                newName: "Latitude");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "gateways",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "altitude",
                table: "gateways",
                newName: "Altitude");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "gateways",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "gateways",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "gateways",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "last_seen_at",
                table: "gateways",
                newName: "LastSeenAt");

            migrationBuilder.RenameColumn(
                name: "gateway_eui",
                table: "gateways",
                newName: "GatewayEui");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "gateways",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_gateways_tenant_id",
                table: "gateways",
                newName: "IX_gateways_TenantId");

            migrationBuilder.RenameIndex(
                name: "ix_gateways_gateway_eui",
                table: "gateways",
                newName: "IX_gateways_GatewayEui");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "devices",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "devices",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "metadata",
                table: "devices",
                newName: "Metadata");

            migrationBuilder.RenameColumn(
                name: "location",
                table: "devices",
                newName: "Location");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "devices",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "devices",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "vpn_public_key",
                table: "devices",
                newName: "VpnPublicKey");

            migrationBuilder.RenameColumn(
                name: "vpn_network_id",
                table: "devices",
                newName: "VpnNetworkId");

            migrationBuilder.RenameColumn(
                name: "vpn_ip_address",
                table: "devices",
                newName: "VpnIpAddress");

            migrationBuilder.RenameColumn(
                name: "vpn_enabled",
                table: "devices",
                newName: "VpnEnabled");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "devices",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "devices",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "signal_strength",
                table: "devices",
                newName: "SignalStrength");

            migrationBuilder.RenameColumn(
                name: "last_seen_at",
                table: "devices",
                newName: "LastSeenAt");

            migrationBuilder.RenameColumn(
                name: "device_profile_id",
                table: "devices",
                newName: "DeviceProfileId");

            migrationBuilder.RenameColumn(
                name: "dev_eui",
                table: "devices",
                newName: "DevEui");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "devices",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "battery_level",
                table: "devices",
                newName: "BatteryLevel");

            migrationBuilder.RenameColumn(
                name: "application_id",
                table: "devices",
                newName: "ApplicationId");

            migrationBuilder.RenameIndex(
                name: "ix_devices_vpn_network_id",
                table: "devices",
                newName: "IX_devices_VpnNetworkId");

            migrationBuilder.RenameIndex(
                name: "ix_devices_tenant_id_dev_eui",
                table: "devices",
                newName: "IX_devices_TenantId_DevEui");

            migrationBuilder.RenameIndex(
                name: "ix_devices_application_id",
                table: "devices",
                newName: "IX_devices_ApplicationId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "applications",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "applications",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "metadata",
                table: "applications",
                newName: "Metadata");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "applications",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "applications",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "applications",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "applications",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "applications",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "chirp_stack_application_id",
                table: "applications",
                newName: "ChirpStackApplicationId");

            migrationBuilder.RenameIndex(
                name: "ix_applications_tenant_id_name",
                table: "applications",
                newName: "IX_applications_TenantId_Name");

            migrationBuilder.AddColumn<string>(
                name: "AutomaisApiPassword",
                table: "routers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutomaisApiUserCreated",
                table: "routers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ConfigContent",
                table: "router_wireguard_peers",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_vpn_networks",
                table: "vpn_networks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_vpn_network_memberships",
                table: "vpn_network_memberships",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenants",
                table: "tenants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenant_users",
                table: "tenant_users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_routers",
                table: "routers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_router_wireguard_peers",
                table: "router_wireguard_peers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_router_config_logs",
                table: "router_config_logs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_router_backups",
                table: "router_backups",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_gateways",
                table: "gateways",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_devices",
                table: "devices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_applications",
                table: "applications",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "router_allowed_networks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouterId = table.Column<Guid>(type: "uuid", nullable: false),
                    NetworkCidr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_router_allowed_networks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_router_allowed_networks_routers_RouterId",
                        column: x => x.RouterId,
                        principalTable: "routers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_router_allowed_networks_RouterId",
                table: "router_allowed_networks",
                column: "RouterId");

            migrationBuilder.CreateIndex(
                name: "IX_router_allowed_networks_RouterId_NetworkCidr",
                table: "router_allowed_networks",
                columns: new[] { "RouterId", "NetworkCidr" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_applications_tenants_TenantId",
                table: "applications",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_applications_ApplicationId",
                table: "devices",
                column: "ApplicationId",
                principalTable: "applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_tenants_TenantId",
                table: "devices",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_vpn_networks_VpnNetworkId",
                table: "devices",
                column: "VpnNetworkId",
                principalTable: "vpn_networks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_gateways_tenants_TenantId",
                table: "gateways",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_router_backups_routers_RouterId",
                table: "router_backups",
                column: "RouterId",
                principalTable: "routers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_router_backups_tenant_users_CreatedByUserId",
                table: "router_backups",
                column: "CreatedByUserId",
                principalTable: "tenant_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_router_backups_tenants_TenantId",
                table: "router_backups",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_router_config_logs_routers_RouterId",
                table: "router_config_logs",
                column: "RouterId",
                principalTable: "routers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_router_config_logs_tenant_users_PortalUserId",
                table: "router_config_logs",
                column: "PortalUserId",
                principalTable: "tenant_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_router_config_logs_tenants_TenantId",
                table: "router_config_logs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_router_wireguard_peers_routers_RouterId",
                table: "router_wireguard_peers",
                column: "RouterId",
                principalTable: "routers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_router_wireguard_peers_vpn_networks_VpnNetworkId",
                table: "router_wireguard_peers",
                column: "VpnNetworkId",
                principalTable: "vpn_networks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_routers_tenants_TenantId",
                table: "routers",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_routers_vpn_networks_VpnNetworkId",
                table: "routers",
                column: "VpnNetworkId",
                principalTable: "vpn_networks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_users_tenants_TenantId",
                table: "tenant_users",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vpn_network_memberships_tenant_users_TenantUserId",
                table: "vpn_network_memberships",
                column: "TenantUserId",
                principalTable: "tenant_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vpn_network_memberships_vpn_networks_VpnNetworkId",
                table: "vpn_network_memberships",
                column: "VpnNetworkId",
                principalTable: "vpn_networks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vpn_networks_tenants_TenantId",
                table: "vpn_networks",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_applications_tenants_TenantId",
                table: "applications");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_applications_ApplicationId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_tenants_TenantId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_vpn_networks_VpnNetworkId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_gateways_tenants_TenantId",
                table: "gateways");

            migrationBuilder.DropForeignKey(
                name: "FK_router_backups_routers_RouterId",
                table: "router_backups");

            migrationBuilder.DropForeignKey(
                name: "FK_router_backups_tenant_users_CreatedByUserId",
                table: "router_backups");

            migrationBuilder.DropForeignKey(
                name: "FK_router_backups_tenants_TenantId",
                table: "router_backups");

            migrationBuilder.DropForeignKey(
                name: "FK_router_config_logs_routers_RouterId",
                table: "router_config_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_router_config_logs_tenant_users_PortalUserId",
                table: "router_config_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_router_config_logs_tenants_TenantId",
                table: "router_config_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_router_wireguard_peers_routers_RouterId",
                table: "router_wireguard_peers");

            migrationBuilder.DropForeignKey(
                name: "FK_router_wireguard_peers_vpn_networks_VpnNetworkId",
                table: "router_wireguard_peers");

            migrationBuilder.DropForeignKey(
                name: "FK_routers_tenants_TenantId",
                table: "routers");

            migrationBuilder.DropForeignKey(
                name: "FK_routers_vpn_networks_VpnNetworkId",
                table: "routers");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_users_tenants_TenantId",
                table: "tenant_users");

            migrationBuilder.DropForeignKey(
                name: "FK_vpn_network_memberships_tenant_users_TenantUserId",
                table: "vpn_network_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_vpn_network_memberships_vpn_networks_VpnNetworkId",
                table: "vpn_network_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_vpn_networks_tenants_TenantId",
                table: "vpn_networks");

            migrationBuilder.DropTable(
                name: "router_allowed_networks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_vpn_networks",
                table: "vpn_networks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_vpn_network_memberships",
                table: "vpn_network_memberships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenants",
                table: "tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenant_users",
                table: "tenant_users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_routers",
                table: "routers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_router_wireguard_peers",
                table: "router_wireguard_peers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_router_config_logs",
                table: "router_config_logs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_router_backups",
                table: "router_backups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_gateways",
                table: "gateways");

            migrationBuilder.DropPrimaryKey(
                name: "PK_devices",
                table: "devices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_applications",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "AutomaisApiPassword",
                table: "routers");

            migrationBuilder.DropColumn(
                name: "AutomaisApiUserCreated",
                table: "routers");

            migrationBuilder.DropColumn(
                name: "ConfigContent",
                table: "router_wireguard_peers");

            migrationBuilder.RenameColumn(
                name: "Slug",
                table: "vpn_networks",
                newName: "slug");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "vpn_networks",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "vpn_networks",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Cidr",
                table: "vpn_networks",
                newName: "cidr");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "vpn_networks",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "vpn_networks",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "vpn_networks",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "IsDefault",
                table: "vpn_networks",
                newName: "is_default");

            migrationBuilder.RenameColumn(
                name: "DnsServers",
                table: "vpn_networks",
                newName: "dns_servers");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "vpn_networks",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_vpn_networks_TenantId_Slug",
                table: "vpn_networks",
                newName: "ix_vpn_networks_tenant_id_slug");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "vpn_network_memberships",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VpnNetworkId",
                table: "vpn_network_memberships",
                newName: "vpn_network_id");

            migrationBuilder.RenameColumn(
                name: "TenantUserId",
                table: "vpn_network_memberships",
                newName: "tenant_user_id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "vpn_network_memberships",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "vpn_network_memberships",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "AssignedIp",
                table: "vpn_network_memberships",
                newName: "assigned_ip");

            migrationBuilder.RenameIndex(
                name: "IX_vpn_network_memberships_VpnNetworkId_TenantUserId",
                table: "vpn_network_memberships",
                newName: "ix_vpn_network_memberships_vpn_network_id_tenant_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_vpn_network_memberships_TenantUserId",
                table: "vpn_network_memberships",
                newName: "ix_vpn_network_memberships_tenant_user_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "tenants",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Slug",
                table: "tenants",
                newName: "slug");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "tenants",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Metadata",
                table: "tenants",
                newName: "metadata");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "tenants",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "tenants",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "tenants",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ChirpStackTenantId",
                table: "tenants",
                newName: "chirp_stack_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                newName: "ix_tenants_slug");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "tenant_users",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "tenant_users",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "tenant_users",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "tenant_users",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "tenant_users",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VpnPublicKey",
                table: "tenant_users",
                newName: "vpn_public_key");

            migrationBuilder.RenameColumn(
                name: "VpnIpAddress",
                table: "tenant_users",
                newName: "vpn_ip_address");

            migrationBuilder.RenameColumn(
                name: "VpnEnabled",
                table: "tenant_users",
                newName: "vpn_enabled");

            migrationBuilder.RenameColumn(
                name: "VpnDeviceName",
                table: "tenant_users",
                newName: "vpn_device_name");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "tenant_users",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "tenant_users",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "LastLoginAt",
                table: "tenant_users",
                newName: "last_login_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "tenant_users",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_tenant_users_TenantId_Email",
                table: "tenant_users",
                newName: "ix_tenant_users_tenant_id_email");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "routers",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "routers",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Model",
                table: "routers",
                newName: "model");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "routers",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "routers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VpnNetworkId",
                table: "routers",
                newName: "vpn_network_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "routers",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "routers",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "SerialNumber",
                table: "routers",
                newName: "serial_number");

            migrationBuilder.RenameColumn(
                name: "RouterOsApiUsername",
                table: "routers",
                newName: "router_os_api_username");

            migrationBuilder.RenameColumn(
                name: "RouterOsApiUrl",
                table: "routers",
                newName: "router_os_api_url");

            migrationBuilder.RenameColumn(
                name: "RouterOsApiPassword",
                table: "routers",
                newName: "router_os_api_password");

            migrationBuilder.RenameColumn(
                name: "LastSeenAt",
                table: "routers",
                newName: "last_seen_at");

            migrationBuilder.RenameColumn(
                name: "HardwareInfo",
                table: "routers",
                newName: "hardware_info");

            migrationBuilder.RenameColumn(
                name: "FirmwareVersion",
                table: "routers",
                newName: "firmware_version");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "routers",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_routers_VpnNetworkId",
                table: "routers",
                newName: "ix_routers_vpn_network_id");

            migrationBuilder.RenameIndex(
                name: "IX_routers_TenantId",
                table: "routers",
                newName: "ix_routers_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_routers_SerialNumber",
                table: "routers",
                newName: "ix_routers_serial_number");

            migrationBuilder.RenameColumn(
                name: "Endpoint",
                table: "router_wireguard_peers",
                newName: "endpoint");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "router_wireguard_peers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VpnNetworkId",
                table: "router_wireguard_peers",
                newName: "vpn_network_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "router_wireguard_peers",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "RouterId",
                table: "router_wireguard_peers",
                newName: "router_id");

            migrationBuilder.RenameColumn(
                name: "PublicKey",
                table: "router_wireguard_peers",
                newName: "public_key");

            migrationBuilder.RenameColumn(
                name: "PrivateKey",
                table: "router_wireguard_peers",
                newName: "private_key");

            migrationBuilder.RenameColumn(
                name: "ListenPort",
                table: "router_wireguard_peers",
                newName: "listen_port");

            migrationBuilder.RenameColumn(
                name: "LastHandshake",
                table: "router_wireguard_peers",
                newName: "last_handshake");

            migrationBuilder.RenameColumn(
                name: "IsEnabled",
                table: "router_wireguard_peers",
                newName: "is_enabled");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "router_wireguard_peers",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "BytesSent",
                table: "router_wireguard_peers",
                newName: "bytes_sent");

            migrationBuilder.RenameColumn(
                name: "BytesReceived",
                table: "router_wireguard_peers",
                newName: "bytes_received");

            migrationBuilder.RenameColumn(
                name: "AllowedIps",
                table: "router_wireguard_peers",
                newName: "allowed_ips");

            migrationBuilder.RenameIndex(
                name: "IX_router_wireguard_peers_VpnNetworkId",
                table: "router_wireguard_peers",
                newName: "ix_router_wireguard_peers_vpn_network_id");

            migrationBuilder.RenameIndex(
                name: "IX_router_wireguard_peers_RouterId_VpnNetworkId",
                table: "router_wireguard_peers",
                newName: "ix_router_wireguard_peers_router_id_vpn_network_id");

            migrationBuilder.RenameIndex(
                name: "IX_router_wireguard_peers_RouterId",
                table: "router_wireguard_peers",
                newName: "ix_router_wireguard_peers_router_id");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "router_config_logs",
                newName: "timestamp");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "router_config_logs",
                newName: "source");

            migrationBuilder.RenameColumn(
                name: "Details",
                table: "router_config_logs",
                newName: "details");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "router_config_logs",
                newName: "category");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "router_config_logs",
                newName: "action");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "router_config_logs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "router_config_logs",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "RouterOsUser",
                table: "router_config_logs",
                newName: "router_os_user");

            migrationBuilder.RenameColumn(
                name: "RouterId",
                table: "router_config_logs",
                newName: "router_id");

            migrationBuilder.RenameColumn(
                name: "PortalUserId",
                table: "router_config_logs",
                newName: "portal_user_id");

            migrationBuilder.RenameColumn(
                name: "ConfigPath",
                table: "router_config_logs",
                newName: "config_path");

            migrationBuilder.RenameColumn(
                name: "BeforeValue",
                table: "router_config_logs",
                newName: "before_value");

            migrationBuilder.RenameColumn(
                name: "AfterValue",
                table: "router_config_logs",
                newName: "after_value");

            migrationBuilder.RenameIndex(
                name: "IX_router_config_logs_Timestamp",
                table: "router_config_logs",
                newName: "ix_router_config_logs_timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_router_config_logs_TenantId",
                table: "router_config_logs",
                newName: "ix_router_config_logs_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_router_config_logs_RouterId",
                table: "router_config_logs",
                newName: "ix_router_config_logs_router_id");

            migrationBuilder.RenameIndex(
                name: "IX_router_config_logs_PortalUserId",
                table: "router_config_logs",
                newName: "ix_router_config_logs_portal_user_id");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "router_backups",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "router_backups",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "router_backups",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "RouterOsVersion",
                table: "router_backups",
                newName: "router_os_version");

            migrationBuilder.RenameColumn(
                name: "RouterModel",
                table: "router_backups",
                newName: "router_model");

            migrationBuilder.RenameColumn(
                name: "RouterId",
                table: "router_backups",
                newName: "router_id");

            migrationBuilder.RenameColumn(
                name: "IsAutomatic",
                table: "router_backups",
                newName: "is_automatic");

            migrationBuilder.RenameColumn(
                name: "FileSizeBytes",
                table: "router_backups",
                newName: "file_size_bytes");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "router_backups",
                newName: "file_path");

            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "router_backups",
                newName: "file_name");

            migrationBuilder.RenameColumn(
                name: "FileHash",
                table: "router_backups",
                newName: "file_hash");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "router_backups",
                newName: "created_by_user_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "router_backups",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CommandCount",
                table: "router_backups",
                newName: "command_count");

            migrationBuilder.RenameIndex(
                name: "IX_router_backups_TenantId",
                table: "router_backups",
                newName: "ix_router_backups_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_router_backups_RouterId",
                table: "router_backups",
                newName: "ix_router_backups_router_id");

            migrationBuilder.RenameIndex(
                name: "IX_router_backups_CreatedByUserId",
                table: "router_backups",
                newName: "ix_router_backups_created_by_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_router_backups_CreatedAt",
                table: "router_backups",
                newName: "ix_router_backups_created_at");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "gateways",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "gateways",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Metadata",
                table: "gateways",
                newName: "metadata");

            migrationBuilder.RenameColumn(
                name: "Longitude",
                table: "gateways",
                newName: "longitude");

            migrationBuilder.RenameColumn(
                name: "Latitude",
                table: "gateways",
                newName: "latitude");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "gateways",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Altitude",
                table: "gateways",
                newName: "altitude");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "gateways",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "gateways",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "gateways",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "LastSeenAt",
                table: "gateways",
                newName: "last_seen_at");

            migrationBuilder.RenameColumn(
                name: "GatewayEui",
                table: "gateways",
                newName: "gateway_eui");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "gateways",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_gateways_TenantId",
                table: "gateways",
                newName: "ix_gateways_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_gateways_GatewayEui",
                table: "gateways",
                newName: "ix_gateways_gateway_eui");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "devices",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "devices",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Metadata",
                table: "devices",
                newName: "metadata");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "devices",
                newName: "location");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "devices",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "devices",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VpnPublicKey",
                table: "devices",
                newName: "vpn_public_key");

            migrationBuilder.RenameColumn(
                name: "VpnNetworkId",
                table: "devices",
                newName: "vpn_network_id");

            migrationBuilder.RenameColumn(
                name: "VpnIpAddress",
                table: "devices",
                newName: "vpn_ip_address");

            migrationBuilder.RenameColumn(
                name: "VpnEnabled",
                table: "devices",
                newName: "vpn_enabled");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "devices",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "devices",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "SignalStrength",
                table: "devices",
                newName: "signal_strength");

            migrationBuilder.RenameColumn(
                name: "LastSeenAt",
                table: "devices",
                newName: "last_seen_at");

            migrationBuilder.RenameColumn(
                name: "DeviceProfileId",
                table: "devices",
                newName: "device_profile_id");

            migrationBuilder.RenameColumn(
                name: "DevEui",
                table: "devices",
                newName: "dev_eui");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "devices",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "BatteryLevel",
                table: "devices",
                newName: "battery_level");

            migrationBuilder.RenameColumn(
                name: "ApplicationId",
                table: "devices",
                newName: "application_id");

            migrationBuilder.RenameIndex(
                name: "IX_devices_VpnNetworkId",
                table: "devices",
                newName: "ix_devices_vpn_network_id");

            migrationBuilder.RenameIndex(
                name: "IX_devices_TenantId_DevEui",
                table: "devices",
                newName: "ix_devices_tenant_id_dev_eui");

            migrationBuilder.RenameIndex(
                name: "IX_devices_ApplicationId",
                table: "devices",
                newName: "ix_devices_application_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "applications",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "applications",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Metadata",
                table: "applications",
                newName: "metadata");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "applications",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "applications",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "applications",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "applications",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "applications",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ChirpStackApplicationId",
                table: "applications",
                newName: "chirp_stack_application_id");

            migrationBuilder.RenameIndex(
                name: "IX_applications_TenantId_Name",
                table: "applications",
                newName: "ix_applications_tenant_id_name");

            migrationBuilder.AddPrimaryKey(
                name: "pk_vpn_networks",
                table: "vpn_networks",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_vpn_network_memberships",
                table: "vpn_network_memberships",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_tenants",
                table: "tenants",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_tenant_users",
                table: "tenant_users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_routers",
                table: "routers",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_router_wireguard_peers",
                table: "router_wireguard_peers",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_router_config_logs",
                table: "router_config_logs",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_router_backups",
                table: "router_backups",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_gateways",
                table: "gateways",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_devices",
                table: "devices",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_applications",
                table: "applications",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_applications_tenants_tenant_id",
                table: "applications",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_devices_applications_application_id",
                table: "devices",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_devices_tenants_tenant_id",
                table: "devices",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_devices_vpn_networks_vpn_network_id",
                table: "devices",
                column: "vpn_network_id",
                principalTable: "vpn_networks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_gateways_tenants_tenant_id",
                table: "gateways",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_router_backups_routers_router_id",
                table: "router_backups",
                column: "router_id",
                principalTable: "routers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_router_backups_tenant_users_created_by_user_id",
                table: "router_backups",
                column: "created_by_user_id",
                principalTable: "tenant_users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_router_backups_tenants_tenant_id",
                table: "router_backups",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_router_config_logs_routers_router_id",
                table: "router_config_logs",
                column: "router_id",
                principalTable: "routers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_router_config_logs_tenant_users_portal_user_id",
                table: "router_config_logs",
                column: "portal_user_id",
                principalTable: "tenant_users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_router_config_logs_tenants_tenant_id",
                table: "router_config_logs",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_router_wireguard_peers_routers_router_id",
                table: "router_wireguard_peers",
                column: "router_id",
                principalTable: "routers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_router_wireguard_peers_vpn_networks_vpn_network_id",
                table: "router_wireguard_peers",
                column: "vpn_network_id",
                principalTable: "vpn_networks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_routers_tenants_tenant_id",
                table: "routers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_routers_vpn_networks_vpn_network_id",
                table: "routers",
                column: "vpn_network_id",
                principalTable: "vpn_networks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tenant_users_tenants_tenant_id",
                table: "tenant_users",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_vpn_network_memberships_tenant_users_tenant_user_id",
                table: "vpn_network_memberships",
                column: "tenant_user_id",
                principalTable: "tenant_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_vpn_network_memberships_vpn_networks_vpn_network_id",
                table: "vpn_network_memberships",
                column: "vpn_network_id",
                principalTable: "vpn_networks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_vpn_networks_tenants_tenant_id",
                table: "vpn_networks",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
