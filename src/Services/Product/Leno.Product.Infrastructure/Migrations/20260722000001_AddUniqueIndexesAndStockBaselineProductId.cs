using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Product.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexesAndStockBaselineProductId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 重建 SKU 索引为唯一索引（修复审计 #2：原为非唯一索引，TOCTOU 竞态下并发请求可双双 Insert）
            migrationBuilder.DropIndex(
                name: "ix_skus_sku_code",
                table: "skus");

            migrationBuilder.CreateIndex(
                name: "ix_skus_sku_code",
                table: "skus",
                column: "sku_code",
                unique: true);

            // 2. 新建 SPU 同店铺内标题唯一复合索引（修复审计 #2：TOCTOU 竞态下并发请求可同时通过唯一性检查然后双双 Insert）
            migrationBuilder.CreateIndex(
                name: "ix_spus_shop_id_title",
                table: "spus",
                columns: new[] { "shop_id", "title" },
                unique: true);

            // 3. 给 stock_baselines 表添加 product_id 列（修复审计 #3：ProductId 字段新增）
            migrationBuilder.AddColumn<Guid>(
                name: "product_id",
                table: "stock_baselines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            // 4. 新建按商品查询库存的非唯一索引
            migrationBuilder.CreateIndex(
                name: "ix_stock_baselines_product_id",
                table: "stock_baselines",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向操作 4：删除按商品查询库存的索引
            migrationBuilder.DropIndex(
                name: "ix_stock_baselines_product_id",
                table: "stock_baselines");

            // 反向操作 3：删除 product_id 列
            migrationBuilder.DropColumn(
                name: "product_id",
                table: "stock_baselines");

            // 反向操作 2：删除 SPU 同店铺内标题唯一复合索引
            migrationBuilder.DropIndex(
                name: "ix_spus_shop_id_title",
                table: "spus");

            // 反向操作 1：重建 SKU 索引为非唯一索引
            migrationBuilder.DropIndex(
                name: "ix_skus_sku_code",
                table: "skus");

            migrationBuilder.CreateIndex(
                name: "ix_skus_sku_code",
                table: "skus",
                column: "sku_code");
        }
    }
}
