using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountVerificationAndRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "credential_version",
                schema: "schema_identity",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateTable(
                name: "account_action_tokens",
                schema: "schema_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_account_action_tokens", x => x.id);
                    table.UniqueConstraint("AK_account_action_tokens_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_identity_account_tokens_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_identity_account_tokens_expiry", "expires_at > issued_at");
                    table.CheckConstraint("ck_identity_account_tokens_purpose", "purpose IN ('VerifyEmail', 'ResetPassword')");
                    table.CheckConstraint("ck_identity_account_tokens_terminal", "consumed_at IS NULL OR revoked_at IS NULL");
                    table.ForeignKey(
                        name: "FK_account_action_tokens_users_tenant_id_user_id",
                        columns: x => new { x.tenant_id, x.user_id },
                        principalSchema: "schema_identity",
                        principalTable: "users",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_email_jobs",
                schema: "schema_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encrypted_payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_account_email_jobs", x => x.id);
                    table.CheckConstraint("ck_identity_account_email_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_account_email_jobs_account_action_tokens_tenant_id_token_id",
                        columns: x => new { x.tenant_id, x.token_id },
                        principalSchema: "schema_identity",
                        principalTable: "account_action_tokens",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_identity_account_tokens_owner_purpose",
                schema: "schema_identity",
                table: "account_action_tokens",
                columns: new[] { "tenant_id", "user_id", "purpose", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ux_identity_account_tokens_hash",
                schema: "schema_identity",
                table: "account_action_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_email_jobs_tenant_id_token_id",
                schema: "schema_identity",
                table: "account_email_jobs",
                columns: new[] { "tenant_id", "token_id" });

            migrationBuilder.CreateIndex(
                name: "ix_identity_account_email_pending",
                schema: "schema_identity",
                table: "account_email_jobs",
                columns: new[] { "next_attempt_at", "id" },
                filter: "completed_at IS NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ux_identity_account_email_token",
                schema: "schema_identity",
                table: "account_email_jobs",
                column: "token_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_email_jobs",
                schema: "schema_identity");

            migrationBuilder.DropTable(
                name: "account_action_tokens",
                schema: "schema_identity");

            migrationBuilder.DropColumn(
                name: "credential_version",
                schema: "schema_identity",
                table: "users");
        }
    }
}
