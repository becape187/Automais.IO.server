using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class vpnpeerdata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PingAvgTimeMs",
                schema: "public",
                table: "router_wireguard_peers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PingPacketLoss",
                schema: "public",
                table: "router_wireguard_peers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PingSuccess",
                schema: "public",
                table: "router_wireguard_peers",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PingAvgTimeMs",
                schema: "public",
                table: "router_wireguard_peers");

            migrationBuilder.DropColumn(
                name: "PingPacketLoss",
                schema: "public",
                table: "router_wireguard_peers");

            migrationBuilder.DropColumn(
                name: "PingSuccess",
                schema: "public",
                table: "router_wireguard_peers");
        }
    }
}
