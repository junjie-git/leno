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
            //
            // 历史缺陷修复（run #12 实证 Msg 2738）：本迁移原时间戳为 20260723100000，排在
            // AddOrderRowVersionAndSoftDelete(20260722000002) 之后——后者 ADD [row_version] rowversion
            // 时 InitialCreate 遗留的 [version] rowversion 尚未删除，SQL Server 单表仅允许一个
            // rowversion 列，空库按序执行迁移/生成脚本必然报 Msg 2738。将本迁移时间戳前移至
            // 20260721999999（介于 InitialCreate 20260717174606 与 20260722000002 之间），
            // 使 DROP version 先于 ADD row_version 执行。迁移语句内容不变，仅排序修正。
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
