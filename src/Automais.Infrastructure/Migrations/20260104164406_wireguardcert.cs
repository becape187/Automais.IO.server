using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class wireguardcert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_routers_SerialNumber",
                table: "routers");

            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.RenameTable(
                name: "vpn_networks",
                newName: "vpn_networks",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "vpn_network_memberships",
                newName: "vpn_network_memberships",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "tenants",
                newName: "tenants",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "tenant_users",
                newName: "tenant_users",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "routers",
                newName: "routers",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "router_wireguard_peers",
                newName: "router_wireguard_peers",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "router_config_logs",
                newName: "router_config_logs",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "router_backups",
                newName: "router_backups",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "router_allowed_networks",
                newName: "router_allowed_networks",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "gateways",
                newName: "gateways",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "devices",
                newName: "devices",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "applications",
                newName: "applications",
                newSchema: "public");

            migrationBuilder.CreateIndex(
                name: "IX_routers_SerialNumber",
                schema: "public",
                table: "routers",
                column: "SerialNumber",
                unique: true,
                filter: "\"SerialNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_routers_SerialNumber",
                schema: "public",
                table: "routers");

            migrationBuilder.RenameTable(
                name: "vpn_networks",
                schema: "public",
                newName: "vpn_networks");

            migrationBuilder.RenameTable(
                name: "vpn_network_memberships",
                schema: "public",
                newName: "vpn_network_memberships");

            migrationBuilder.RenameTable(
                name: "tenants",
                schema: "public",
                newName: "tenants");

            migrationBuilder.RenameTable(
                name: "tenant_users",
                schema: "public",
                newName: "tenant_users");

            migrationBuilder.RenameTable(
                name: "routers",
                schema: "public",
                newName: "routers");

            migrationBuilder.RenameTable(
                name: "router_wireguard_peers",
                schema: "public",
                newName: "router_wireguard_peers");

            migrationBuilder.RenameTable(
                name: "router_config_logs",
                schema: "public",
                newName: "router_config_logs");

            migrationBuilder.RenameTable(
                name: "router_backups",
                schema: "public",
                newName: "router_backups");

            migrationBuilder.RenameTable(
                name: "router_allowed_networks",
                schema: "public",
                newName: "router_allowed_networks");

            migrationBuilder.RenameTable(
                name: "gateways",
                schema: "public",
                newName: "gateways");

            migrationBuilder.RenameTable(
                name: "devices",
                schema: "public",
                newName: "devices");

            migrationBuilder.RenameTable(
                name: "applications",
                schema: "public",
                newName: "applications");

            migrationBuilder.CreateIndex(
                name: "IX_routers_SerialNumber",
                table: "routers",
                column: "SerialNumber",
                unique: true,
                filter: "\"serial_number\" IS NOT NULL");
        }
    }
}
