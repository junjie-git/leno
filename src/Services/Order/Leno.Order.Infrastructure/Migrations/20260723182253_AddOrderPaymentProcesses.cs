using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaymentProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_payment_processes",
                columns: table => new
                {
                    process_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    payment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    current_state = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    stock_confirmed = table.Column<bool>(type: "bit", nullable: false),
                    points_confirmed = table.Column<bool>(type: "bit", nullable: false),
                    order_marked_paid = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payment_processes", x => x.process_id);
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_payment_processes");
        }
    }
}
