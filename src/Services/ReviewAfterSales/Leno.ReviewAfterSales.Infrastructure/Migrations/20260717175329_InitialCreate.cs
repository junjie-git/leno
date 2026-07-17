using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.ReviewAfterSales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "after_sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    seller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    reason_category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    images = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    requested_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    approved_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    refunded_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    applied_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    channel_refund_no = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    reject_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    fail_reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cancel_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    returned_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    tracking_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    return_confirmed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_after_sales", x => x.id);
                });

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
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    spu_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rating = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    images = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    seller_reply_content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    audited_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    auditor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    hidden_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    hidden_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    hide_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_after_sales_order_id",
                table: "after_sales",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_after_sales_seller_id",
                table: "after_sales",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "ix_after_sales_status",
                table: "after_sales",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_after_sales_user_id",
                table: "after_sales",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_order_line_id",
                table: "reviews",
                column: "order_line_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_spu_id",
                table: "reviews",
                column: "spu_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user_id",
                table: "reviews",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "after_sales");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "reviews");
        }
    }
}
