using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationEmailJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_user_invitations_tenant_id_id",
                schema: "schema_identity",
                table: "user_invitations",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "invitation_email_jobs",
                schema: "schema_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_invitation_email_jobs", x => x.id);
                    table.CheckConstraint("ck_identity_email_jobs_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_invitation_email_jobs_user_invitations_tenant_id_invitation~",
                        columns: x => new { x.tenant_id, x.invitation_id },
                        principalSchema: "schema_identity",
                        principalTable: "user_invitations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_identity_email_jobs_pending",
                schema: "schema_identity",
                table: "invitation_email_jobs",
                columns: new[] { "next_attempt_at", "id" },
                filter: "completed_at IS NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_email_jobs_tenant_id_invitation_id",
                schema: "schema_identity",
                table: "invitation_email_jobs",
                columns: new[] { "tenant_id", "invitation_id" });

            migrationBuilder.CreateIndex(
                name: "ux_identity_email_jobs_invitation",
                schema: "schema_identity",
                table: "invitation_email_jobs",
                column: "invitation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitation_email_jobs",
                schema: "schema_identity");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_user_invitations_tenant_id_id",
                schema: "schema_identity",
                table: "user_invitations");
        }
    }
}
