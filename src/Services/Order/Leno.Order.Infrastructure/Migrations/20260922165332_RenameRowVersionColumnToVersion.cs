using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameRowVersionColumnToVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "row_version",
                table: "orders",
                newName: "version");

            migrationBuilder.RenameColumn(
                name: "row_version",
                table: "order_saga_states",
                newName: "version");

            migrationBuilder.RenameColumn(
                name: "row_version",
                table: "order_payment_processes",
                newName: "version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "version",
                table: "orders",
                newName: "row_version");

            migrationBuilder.RenameColumn(
                name: "version",
                table: "order_saga_states",
                newName: "row_version");

            migrationBuilder.RenameColumn(
                name: "version",
                table: "order_payment_processes",
                newName: "row_version");
        }
    }
}
