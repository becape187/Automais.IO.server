using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class router : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "routers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    firmware_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    router_os_api_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    router_os_api_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    router_os_api_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    vpn_network_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    hardware_info = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_routers", x => x.id);
                    table.ForeignKey(
                        name: "fk_routers_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_routers_vpn_networks_vpn_network_id",
                        column: x => x.vpn_network_id,
                        principalTable: "vpn_networks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "router_backups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    router_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    router_model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    router_os_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    command_count = table.Column<int>(type: "integer", nullable: false),
                    file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_automatic = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_router_backups", x => x.id);
                    table.ForeignKey(
                        name: "fk_router_backups_routers_router_id",
                        column: x => x.router_id,
                        principalTable: "routers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_router_backups_tenant_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_router_backups_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "router_config_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    router_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    portal_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    router_os_user = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    config_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    before_value = table.Column<string>(type: "text", nullable: true),
                    after_value = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_router_config_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_router_config_logs_routers_router_id",
                        column: x => x.router_id,
                        principalTable: "routers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_router_config_logs_tenant_users_portal_user_id",
                        column: x => x.portal_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_router_config_logs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "router_wireguard_peers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    router_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vpn_network_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    private_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    allowed_ips = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    listen_port = table.Column<int>(type: "integer", nullable: true),
                    last_handshake = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    bytes_received = table.Column<long>(type: "bigint", nullable: true),
                    bytes_sent = table.Column<long>(type: "bigint", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_router_wireguard_peers", x => x.id);
                    table.ForeignKey(
                        name: "fk_router_wireguard_peers_routers_router_id",
                        column: x => x.router_id,
                        principalTable: "routers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_router_wireguard_peers_vpn_networks_vpn_network_id",
                        column: x => x.vpn_network_id,
                        principalTable: "vpn_networks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_router_backups_created_at",
                table: "router_backups",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_router_backups_created_by_user_id",
                table: "router_backups",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_router_backups_router_id",
                table: "router_backups",
                column: "router_id");

            migrationBuilder.CreateIndex(
                name: "ix_router_backups_tenant_id",
                table: "router_backups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_router_config_logs_portal_user_id",
                table: "router_config_logs",
                column: "portal_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_router_config_logs_router_id",
                table: "router_config_logs",
                column: "router_id");

            migrationBuilder.CreateIndex(
                name: "ix_router_config_logs_tenant_id",
                table: "router_config_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_router_config_logs_timestamp",
                table: "router_config_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_router_wireguard_peers_router_id",
                table: "router_wireguard_peers",
                column: "router_id");

            migrationBuilder.CreateIndex(
                name: "ix_router_wireguard_peers_router_id_vpn_network_id",
                table: "router_wireguard_peers",
                columns: new[] { "router_id", "vpn_network_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_router_wireguard_peers_vpn_network_id",
                table: "router_wireguard_peers",
                column: "vpn_network_id");

            migrationBuilder.CreateIndex(
                name: "ix_routers_serial_number",
                table: "routers",
                column: "serial_number",
                unique: true,
                filter: "\"serial_number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_routers_tenant_id",
                table: "routers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_routers_vpn_network_id",
                table: "routers",
                column: "vpn_network_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "router_backups");

            migrationBuilder.DropTable(
                name: "router_config_logs");

            migrationBuilder.DropTable(
                name: "router_wireguard_peers");

            migrationBuilder.DropTable(
                name: "routers");
        }
    }
}
