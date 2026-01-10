using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class vpnroutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "router_static_routes",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Destination = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Gateway = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Interface = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Distance = table.Column<int>(type: "integer", nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: true),
                    RoutingTable = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Comment = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RouterOsId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_router_static_routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_router_static_routes_routers_RouterId",
                        column: x => x.RouterId,
                        principalSchema: "public",
                        principalTable: "routers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_router_static_routes_RouterId",
                schema: "public",
                table: "router_static_routes",
                column: "RouterId");

            migrationBuilder.CreateIndex(
                name: "IX_router_static_routes_RouterId_Destination",
                schema: "public",
                table: "router_static_routes",
                columns: new[] { "RouterId", "Destination" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "router_static_routes",
                schema: "public");
        }
    }
}
