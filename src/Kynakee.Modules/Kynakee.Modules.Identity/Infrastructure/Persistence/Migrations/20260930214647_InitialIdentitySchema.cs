using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "schema_identity");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "schema_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.CheckConstraint("ck_identity_refresh_tokens_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "schema_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tax_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    fiscal_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    fiscal_region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fiscal_province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fiscal_municipality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fiscal_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    fiscal_street = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    plan_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    branding_company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    branding_primary_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    branding_logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    branding_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    settings_administration = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    settings_profit = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    settings_quality = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    settings_safety_health = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    settings_environment = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    settings_contingency = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_tenants", x => x.id);
                    table.UniqueConstraint("ak_identity_tenants_tenant_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_identity_tenants_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_identity_tenants_tenant_id", "tenant_id = id");
                });

            migrationBuilder.CreateTable(
                name: "user_invitations",
                schema: "schema_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_user_invitations", x => x.id);
                    table.CheckConstraint("ck_identity_user_invitations_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "schema_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_users", x => x.id);
                    table.UniqueConstraint("ak_identity_users_tenant_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_identity_users_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "tenant_users",
                schema: "schema_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_tenant_users", x => x.id);
                    table.CheckConstraint("ck_identity_tenant_users_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_tenant_users_users_tenant_id_user_id",
                        columns: x => new { x.tenant_id, x.user_id },
                        principalSchema: "schema_identity",
                        principalTable: "users",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_identity_refresh_tokens_owner_expiry",
                schema: "schema_identity",
                table: "refresh_tokens",
                columns: new[] { "tenant_id", "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_identity_refresh_tokens_hash",
                schema: "schema_identity",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_identity_tenant_users_tenant_role",
                schema: "schema_identity",
                table: "tenant_users",
                columns: new[] { "tenant_id", "role" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_tenant_id_user_id",
                schema: "schema_identity",
                table: "tenant_users",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_identity_tenant_users_user",
                schema: "schema_identity",
                table: "tenant_users",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_identity_tenants_slug",
                schema: "schema_identity",
                table: "tenants",
                column: "slug",
                unique: true,
                filter: "\"is_deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_identity_invitations_owner_expiry",
                schema: "schema_identity",
                table: "user_invitations",
                columns: new[] { "tenant_id", "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_identity_invitations_hash",
                schema: "schema_identity",
                table: "user_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_identity_users_tenant_status",
                schema: "schema_identity",
                table: "users",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_normalized_email",
                schema: "schema_identity",
                table: "users",
                column: "normalized_email",
                unique: true,
                filter: "\"is_deleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "schema_identity");

            migrationBuilder.DropTable(
                name: "tenant_users",
                schema: "schema_identity");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "schema_identity");

            migrationBuilder.DropTable(
                name: "user_invitations",
                schema: "schema_identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "schema_identity");
        }
    }
}
