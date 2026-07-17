using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Payment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    publishing_started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    retry_count = table.Column<int>(type: "int", nullable: false),
                    error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_channel_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    channel = table.Column<int>(type: "int", nullable: false),
                    config_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    config_value = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_channel_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    out_trade_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    channel = table.Column<int>(type: "int", nullable: false),
                    channel_trade_no = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    prepay_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    code_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    h5_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    expire_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    paid_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    fail_reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationDiffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DiffType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChannelTransactionNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ChannelAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ChannelTransactionTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SystemTransactionNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SystemAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationDiffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "refund_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    out_refund_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    out_trade_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    payment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    after_sales_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    refund_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    channel = table.Column<int>(type: "int", nullable: false),
                    channel_refund_no = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    refunded_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    fail_reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_payment_channel_configs_channel",
                table: "payment_channel_configs",
                column: "channel");

            migrationBuilder.CreateIndex(
                name: "ix_payment_channel_configs_channel_name",
                table: "payment_channel_configs",
                columns: new[] { "channel", "config_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_orders_order_id",
                table: "payment_orders",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_orders_out_trade_no",
                table: "payment_orders",
                column: "out_trade_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_orders_status",
                table: "payment_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_payment_orders_user_id",
                table: "payment_orders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationDiffs_BillDate",
                table: "ReconciliationDiffs",
                column: "BillDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationDiffs_BillDate_Channel",
                table: "ReconciliationDiffs",
                columns: new[] { "BillDate", "Channel" });

            migrationBuilder.CreateIndex(
                name: "ix_refund_orders_after_sales_id",
                table: "refund_orders",
                column: "after_sales_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_orders_order_id",
                table: "refund_orders",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_orders_out_refund_no",
                table: "refund_orders",
                column: "out_refund_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refund_orders_payment_id",
                table: "refund_orders",
                column: "payment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "payment_channel_configs");

            migrationBuilder.DropTable(
                name: "payment_orders");

            migrationBuilder.DropTable(
                name: "ReconciliationDiffs");

            migrationBuilder.DropTable(
                name: "refund_orders");
        }
    }
}
