using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kynakee.Modules.Bots.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBotsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "schema_bots");

            migrationBuilder.CreateTable(
                name: "bot_conversations",
                schema: "schema_bots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'NewSession'"),
                    last_interaction_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verbosity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Normal'"),
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
                    table.PrimaryKey("PK_bot_conversations", x => x.id);
                    table.UniqueConstraint("ak_bot_conversations_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_bot_conversations_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "bot_messages",
                schema: "schema_bots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    message_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    media_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    parsed_command = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_bot_messages", x => x.id);
                    table.CheckConstraint("ck_bot_messages_content", "content IS NOT NULL OR media_url IS NOT NULL");
                    table.CheckConstraint("ck_bot_messages_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_bot_messages_media_url_https", "media_url IS NULL OR (media_url LIKE 'https://%' AND length(media_url) <= 500)");
                    table.ForeignKey(
                        name: "FK_bot_messages_bot_conversations_tenant_id_conversation_id",
                        columns: x => new { x.tenant_id, x.conversation_id },
                        principalSchema: "schema_bots",
                        principalTable: "bot_conversations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_bot_conversations_external",
                schema: "schema_bots",
                table: "bot_conversations",
                columns: new[] { "tenant_id", "channel", "external_id" },
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "idx_bot_messages_conversation",
                schema: "schema_bots",
                table: "bot_messages",
                columns: new[] { "tenant_id", "conversation_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_messages",
                schema: "schema_bots");

            migrationBuilder.DropTable(
                name: "bot_conversations",
                schema: "schema_bots");
        }
    }
}
