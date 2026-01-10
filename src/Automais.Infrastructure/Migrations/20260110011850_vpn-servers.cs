using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class vpnservers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VpnServerId",
                schema: "public",
                table: "vpn_networks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vpn_servers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ServerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SshPort = table.Column<int>(type: "integer", nullable: false, defaultValue: 22),
                    SshUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SshPassword = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SshKeyPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vpn_servers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vpn_networks_VpnServerId",
                schema: "public",
                table: "vpn_networks",
                column: "VpnServerId");

            migrationBuilder.CreateIndex(
                name: "IX_vpn_servers_ServerName",
                schema: "public",
                table: "vpn_servers",
                column: "ServerName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_vpn_networks_vpn_servers_VpnServerId",
                schema: "public",
                table: "vpn_networks",
                column: "VpnServerId",
                principalSchema: "public",
                principalTable: "vpn_servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vpn_networks_vpn_servers_VpnServerId",
                schema: "public",
                table: "vpn_networks");

            migrationBuilder.DropTable(
                name: "vpn_servers",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_vpn_networks_VpnServerId",
                schema: "public",
                table: "vpn_networks");

            migrationBuilder.DropColumn(
                name: "VpnServerId",
                schema: "public",
                table: "vpn_networks");
        }
    }
}
