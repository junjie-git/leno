using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropOrderVersionShadowColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 删除 orders 表的 shadow property 列 version（由 BaseDbContext 统一注入的 rowversion shadow 列）。
            // OrderConfiguration 已显式声明 row_version 列为 IsRowVersion，SQL Server 单表仅允许一个 rowversion 列，
            // 因此移除冗余的 version 列，保留显式声明的 row_version 列作为并发控制令牌（NEW-P0-1）。
            migrationBuilder.DropColumn(
                name: "version",
                table: "orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 恢复 orders 表的 shadow property 列 version（rowversion 类型，可空）。
            migrationBuilder.AddColumn<byte[]>(
                name: "version",
                table: "orders",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }
    }
}
