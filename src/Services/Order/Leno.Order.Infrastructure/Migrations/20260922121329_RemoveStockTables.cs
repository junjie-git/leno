using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStockTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_reservation_compensations");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropColumn(
                name: "stock_reservation_ids_json",
                table: "order_saga_states");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "stock_reservation_ids_json",
                table: "order_saga_states",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_reservation_compensations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    last_attempted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_error_message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    max_retries = table.Column<int>(type: "int", nullable: false),
                    operation_type = table.Column<int>(type: "int", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    retry_count = table.Column<int>(type: "int", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservation_compensations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    base_line_qty = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    deducted_qty = table.Column<int>(type: "int", nullable: false),
                    reserved_qty = table.Column<int>(type: "int", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_compensations_order_id",
                table: "stock_reservation_compensations",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_compensations_order_sku_pending",
                table: "stock_reservation_compensations",
                columns: new[] { "order_id", "sku_id" },
                unique: true,
                filter: "[status] = 0");

            migrationBuilder.CreateIndex(
                name: "ix_stock_compensations_status",
                table: "stock_reservation_compensations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_sku_id",
                table: "stock_reservations",
                column: "sku_id",
                unique: true);
        }
    }
}
