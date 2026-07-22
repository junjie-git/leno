using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderRowVersionAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 乐观并发控制：row_version 列由 SQL Server 自动维护，并发写入时触发 DbUpdateConcurrencyException
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "orders",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            // 软删除标记列，默认 false 表示未删除（P1-T26）
            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // 软删除时间列（UTC），未删除时为 null（P1-T26）
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "orders",
                type: "datetime2",
                nullable: true);

            // 软删除过滤索引，加速 IsDeleted=false 默认查询路径
            migrationBuilder.CreateIndex(
                name: "ix_orders_is_deleted",
                table: "orders",
                column: "is_deleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_is_deleted",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "orders");
        }
    }
}
