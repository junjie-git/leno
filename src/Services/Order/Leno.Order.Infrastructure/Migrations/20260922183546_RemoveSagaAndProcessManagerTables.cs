using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSagaAndProcessManagerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_payment_processes");

            migrationBuilder.DropTable(
                name: "order_saga_states");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_payment_processes",
                columns: table => new
                {
                    process_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    current_state = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_marked_paid = table.Column<bool>(type: "bit", nullable: false),
                    payment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    points_confirmed = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    stock_confirmed = table.Column<bool>(type: "bit", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payment_processes", x => x.process_id);
                });

            migrationBuilder.CreateTable(
                name: "order_saga_states",
                columns: table => new
                {
                    correlation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    current_state = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    items_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    payment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    points_frozen_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_saga_states", x => x.correlation_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_payment_processes_current_state",
                table: "order_payment_processes",
                column: "current_state");

            migrationBuilder.CreateIndex(
                name: "ix_order_payment_processes_order_id",
                table: "order_payment_processes",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_saga_states_current_state",
                table: "order_saga_states",
                column: "current_state");
        }
    }
}
