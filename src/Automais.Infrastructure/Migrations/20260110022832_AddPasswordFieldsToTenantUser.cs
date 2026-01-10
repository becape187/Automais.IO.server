using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Automais.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordFieldsToTenantUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                schema: "public",
                table: "tenant_users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemporaryPassword",
                schema: "public",
                table: "tenant_users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TemporaryPasswordExpiresAt",
                schema: "public",
                table: "tenant_users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                schema: "public",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "TemporaryPassword",
                schema: "public",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "TemporaryPasswordExpiresAt",
                schema: "public",
                table: "tenant_users");
        }
    }
}
