using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Product.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceHistoryAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "average_score",
                table: "spus");

            migrationBuilder.DropColumn(
                name: "price_change_history",
                table: "spus");

            migrationBuilder.DropColumn(
                name: "review_count",
                table: "spus");

            migrationBuilder.DropColumn(
                name: "stock_operation_history",
                table: "spus");

            migrationBuilder.CreateTable(
                name: "price_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    spu_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    old_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    new_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    changed_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_histories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_price_histories_sku_id",
                table: "price_histories",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_histories_spu_changed_at",
                table: "price_histories",
                columns: new[] { "spu_id", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_histories");

            migrationBuilder.AddColumn<double>(
                name: "average_score",
                table: "spus",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "price_change_history",
                table: "spus",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "review_count",
                table: "spus",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "stock_operation_history",
                table: "spus",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
