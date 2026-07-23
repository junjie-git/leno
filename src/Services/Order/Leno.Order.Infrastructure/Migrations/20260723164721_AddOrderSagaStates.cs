using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSagaStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "schema_version",
                table: "outbox_messages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<byte[]>(
                name: "row_version",
                table: "orders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "order_saga_states",
                columns: table => new
                {
                    correlation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    current_state = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    items_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    stock_reservation_ids_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    points_frozen_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    payment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_saga_states", x => x.correlation_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_compensations_order_sku_pending",
                table: "stock_reservation_compensations",
                columns: new[] { "order_id", "sku_id" },
                unique: true,
                filter: "[status] = 0");

            migrationBuilder.CreateIndex(
                name: "ix_order_saga_states_current_state",
                table: "order_saga_states",
                column: "current_state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_saga_states");

            migrationBuilder.DropIndex(
                name: "ix_stock_compensations_order_sku_pending",
                table: "stock_reservation_compensations");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_messages");

            migrationBuilder.AlterColumn<byte[]>(
                name: "row_version",
                table: "orders",
                type: "rowversion",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);
        }
    }
}
