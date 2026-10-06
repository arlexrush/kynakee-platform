using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kynakee.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBillingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "schema_billing");

            migrationBuilder.CreateTable(
                name: "credit_accounts",
                schema: "schema_billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_credit_accounts", x => x.id);
                    table.UniqueConstraint("ak_billing_credit_accounts_tenant_id", x => x.tenant_id);
                    table.CheckConstraint("ck_billing_credit_accounts_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "credit_lots",
                schema: "schema_billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    available_credits = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reserved_credits = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    purchased_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_credit_lots", x => x.id);
                    table.CheckConstraint("ck_billing_credit_lots_balance", "amount >= 0 AND available_credits >= 0 AND reserved_credits >= 0 AND amount = available_credits + reserved_credits");
                    table.CheckConstraint("ck_billing_credit_lots_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_billing_credit_lots_expiry", "expires_at >= purchased_at + interval '3 months'");
                    table.ForeignKey(
                        name: "FK_credit_lots_credit_accounts_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "schema_billing",
                        principalTable: "credit_accounts",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_transactions",
                schema: "schema_billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_credit_transactions", x => x.id);
                    table.CheckConstraint("ck_billing_credit_transactions_amount", "amount > 0");
                    table.CheckConstraint("ck_billing_credit_transactions_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_billing_credit_transactions_operation", "type = 'Recharged' OR operation_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_credit_transactions_credit_accounts_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "schema_billing",
                        principalTable: "credit_accounts",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "schema_billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    stripe_subscription_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    credits_per_cycle = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                    table.CheckConstraint("ck_billing_subscriptions_credits", "credits_per_cycle > 0");
                    table.CheckConstraint("ck_billing_subscriptions_deleted", "is_deleted = (deleted_at IS NOT NULL)");
                    table.CheckConstraint("ck_billing_subscriptions_period", "current_period_end > current_period_start");
                    table.ForeignKey(
                        name: "FK_subscriptions_credit_accounts_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "schema_billing",
                        principalTable: "credit_accounts",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_transaction_allocations",
                schema: "schema_billing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    credit_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    credit_transaction_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_transaction_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_transaction_allocations_credit_transactions_credit_t~",
                        column: x => x.credit_transaction_id,
                        principalSchema: "schema_billing",
                        principalTable: "credit_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_billing_credit_lots_tenant_expiry",
                schema: "schema_billing",
                table: "credit_lots",
                columns: new[] { "tenant_id", "expires_at", "purchased_at" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_transaction_allocations_credit_transaction_id",
                schema: "schema_billing",
                table: "credit_transaction_allocations",
                column: "credit_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_billing_credit_transactions_history",
                schema: "schema_billing",
                table: "credit_transactions",
                columns: new[] { "tenant_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_billing_credit_transactions_operation",
                schema: "schema_billing",
                table: "credit_transactions",
                columns: new[] { "tenant_id", "operation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_tenant_id",
                schema: "schema_billing",
                table: "subscriptions",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_billing_subscriptions_stripe_id",
                schema: "schema_billing",
                table: "subscriptions",
                column: "stripe_subscription_id",
                unique: true,
                filter: "stripe_subscription_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_lots",
                schema: "schema_billing");

            migrationBuilder.DropTable(
                name: "credit_transaction_allocations",
                schema: "schema_billing");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "schema_billing");

            migrationBuilder.DropTable(
                name: "credit_transactions",
                schema: "schema_billing");

            migrationBuilder.DropTable(
                name: "credit_accounts",
                schema: "schema_billing");
        }
    }
}
