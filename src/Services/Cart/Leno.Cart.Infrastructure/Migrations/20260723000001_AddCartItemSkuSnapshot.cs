using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Cart.Infrastructure.Migrations
{
    /// <summary>
    /// 阶段三 3.11：为 cart_items 表新增 SKU 快照列（sku_snapshot_*）。
    /// <para>
    /// SkuSnapshot 作为 EF Core owned entity 映射到 cart_items 表的 sku_snapshot_* 列，
    /// 所有列可空，允许历史购物车项渐进回填。Ownership 设为可选（SkuSnapshot 可为 null），
    /// EF Core 在读取时若所有 owned 列均为 NULL，则将 SkuSnapshot 设为 null。
    /// </para>
    /// </summary>
    public partial class AddCartItemSkuSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SKU 快照列：全部可空，支持历史数据渐进回填
            migrationBuilder.AddColumn<Guid>(
                name: "sku_snapshot_sku_id",
                table: "cart_items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sku_snapshot_sku_name",
                table: "cart_items",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sku_snapshot_price",
                table: "cart_items",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sku_snapshot_currency",
                table: "cart_items",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sku_snapshot_main_image_url",
                table: "cart_items",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sku_snapshot_spec_text",
                table: "cart_items",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sku_snapshot_available",
                table: "cart_items",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sku_snapshot_version",
                table: "cart_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sku_snapshot_at",
                table: "cart_items",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sku_snapshot_sku_id",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "sku_snapshot_sku_name",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "sku_snapshot_price",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "sku_snapshot_currency",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "sku_snapshot_main_image_url",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "sku_snapshot_spec_text",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "sku_snapshot_available",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "sku_snapshot_version",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "sku_snapshot_at",
                table: "cart_items");
        }
    }
}
