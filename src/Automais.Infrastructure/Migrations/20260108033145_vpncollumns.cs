using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class vpncollumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VpnPrivateKey",
                schema: "public",
                table: "tenant_users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_allowed_routes",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouterAllowedNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    NetworkCidr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_allowed_routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_allowed_routes_router_allowed_networks_RouterAllowedNe~",
                        column: x => x.RouterAllowedNetworkId,
                        principalSchema: "public",
                        principalTable: "router_allowed_networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_allowed_routes_routers_RouterId",
                        column: x => x.RouterId,
                        principalSchema: "public",
                        principalTable: "routers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_allowed_routes_tenant_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "tenant_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_allowed_routes_RouterAllowedNetworkId",
                schema: "public",
                table: "user_allowed_routes",
                column: "RouterAllowedNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_user_allowed_routes_RouterId",
                schema: "public",
                table: "user_allowed_routes",
                column: "RouterId");

            migrationBuilder.CreateIndex(
                name: "IX_user_allowed_routes_UserId",
                schema: "public",
                table: "user_allowed_routes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_allowed_routes_UserId_RouterAllowedNetworkId",
                schema: "public",
                table: "user_allowed_routes",
                columns: new[] { "UserId", "RouterAllowedNetworkId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_allowed_routes",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "VpnPrivateKey",
                schema: "public",
                table: "tenant_users");
        }
    }
}
