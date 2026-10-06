using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kynakee.Modules.Mcp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptionalMcpQueryProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "project_id",
                schema: "schema_mcp",
                table: "mcp_query_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "project_id",
                schema: "schema_mcp",
                table: "mcp_query_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
