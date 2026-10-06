using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kynakee.Modules.Mcp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMcpSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "schema_mcp");

            migrationBuilder.CreateTable(
                name: "mcp_providers",
                schema: "schema_mcp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    last_success_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suspended_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    categories = table.Column<List<string>>(type: "text[]", nullable: false),
                    geo_regions = table.Column<List<string>>(type: "text[]", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_providers", x => x.id);
                    table.UniqueConstraint("ak_mcp_providers_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_mcp_providers_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_mcp_providers_failures", "consecutive_failures >= 0");
                    table.CheckConstraint("ck_mcp_providers_rating", "rating >= 0 AND rating <= 5");
                });

            migrationBuilder.CreateTable(
                name: "mcp_query_logs",
                schema: "schema_mcp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canonical_concept_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    component_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_price = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    response_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    fallback_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    confidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    credits_charged = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    queried_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_query_logs", x => x.id);
                    table.CheckConstraint("ck_mcp_query_logs_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                    table.CheckConstraint("ck_mcp_query_logs_credits", "credits_charged >= 0");
                    table.CheckConstraint("ck_mcp_query_logs_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_mcp_query_logs_duration", "duration_ms IS NULL OR duration_ms >= 0");
                    table.CheckConstraint("ck_mcp_query_logs_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_mcp_query_logs_mcp_providers_provider_id",
                        column: x => x.provider_id,
                        principalSchema: "schema_mcp",
                        principalTable: "mcp_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mcp_servers",
                schema: "schema_mcp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    credential_secret_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_servers", x => x.id);
                    table.CheckConstraint("ck_mcp_servers_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_mcp_servers_endpoint_https", "endpoint LIKE 'https://%'");
                    table.ForeignKey(
                        name: "FK_mcp_servers_mcp_providers_tenant_id_provider_id",
                        columns: x => new { x.tenant_id, x.provider_id },
                        principalSchema: "schema_mcp",
                        principalTable: "mcp_providers",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_mcp_providers_category",
                schema: "schema_mcp",
                table: "mcp_providers",
                column: "categories")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_mcp_providers_region",
                schema: "schema_mcp",
                table: "mcp_providers",
                column: "geo_regions")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_mcp_providers_tenant_status",
                schema: "schema_mcp",
                table: "mcp_providers",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_mcp_query_logs_project",
                schema: "schema_mcp",
                table: "mcp_query_logs",
                columns: new[] { "tenant_id", "project_id", "queried_at" });

            migrationBuilder.CreateIndex(
                name: "idx_mcp_query_logs_provider",
                schema: "schema_mcp",
                table: "mcp_query_logs",
                columns: new[] { "tenant_id", "provider_id", "queried_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_query_logs_provider_id",
                schema: "schema_mcp",
                table: "mcp_query_logs",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "idx_mcp_servers_endpoint",
                schema: "schema_mcp",
                table: "mcp_servers",
                column: "endpoint",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "idx_mcp_servers_provider",
                schema: "schema_mcp",
                table: "mcp_servers",
                columns: new[] { "tenant_id", "provider_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mcp_query_logs",
                schema: "schema_mcp");

            migrationBuilder.DropTable(
                name: "mcp_servers",
                schema: "schema_mcp");

            migrationBuilder.DropTable(
                name: "mcp_providers",
                schema: "schema_mcp");
        }
    }
}
