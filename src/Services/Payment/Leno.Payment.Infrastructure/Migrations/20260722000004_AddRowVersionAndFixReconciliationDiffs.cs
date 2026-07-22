using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Payment.Infrastructure.Migrations
{
    /// <summary>
    /// 补齐 PaymentOrder/RefundOrder 的 RowVersion 显式并发令牌（列名由 version 改为 row_version），
    /// 并修正 ReconciliationDiffs 表名与枚举列类型（snake_case 表名 + int 存储），使其与 Configuration 一致。
    /// </summary>
    public partial class AddRowVersionAndFixReconciliationDiffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1a. payment_orders：先删除旧 Version shadow 列，再添加 RowVersion 显式列。
            //     顺序必须为先删后加：SQL Server 单表仅允许一个 rowversion 列，
            //     若先添加 row_version 会与既有 version 形成“两个 rowversion 列”冲突。
            migrationBuilder.DropColumn(
                name: "version",
                table: "payment_orders");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "payment_orders",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            // 1b. refund_orders：同上，先删旧 version 再加新 row_version。
            migrationBuilder.DropColumn(
                name: "version",
                table: "refund_orders");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "refund_orders",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            // 1c. ReconciliationDiffs 表名改为 snake_case，与 payment_orders / refund_orders 等保持一致。
            migrationBuilder.RenameTable(
                name: "ReconciliationDiffs",
                newName: "reconciliation_diffs");

            // 1d. Channel / DiffType / Status 由 nvarchar 改为 int，与 HasConversion<int>() 配置对齐。
            //     注意：此时表名已改为 reconciliation_diffs，AlterColumn 引用新表名。
            migrationBuilder.AlterColumn<int>(
                name: "Channel",
                table: "reconciliation_diffs",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "DiffType",
                table: "reconciliation_diffs",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "reconciliation_diffs",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向操作：将 int 改回 nvarchar，恢复表名，再将 row_version 还原为 version。
            // 顺序与 Up 相反：先 AlterColumn，再 RenameTable，最后处理 rowversion 列。
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "reconciliation_diffs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "DiffType",
                table: "reconciliation_diffs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "reconciliation_diffs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // 恢复表名为 PascalCase 的 ReconciliationDiffs。
            migrationBuilder.RenameTable(
                name: "reconciliation_diffs",
                newName: "ReconciliationDiffs");

            // refund_orders：先删 row_version 再加 version，避免两个 rowversion 列冲突。
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "refund_orders");

            migrationBuilder.AddColumn<byte[]>(
                name: "version",
                table: "refund_orders",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            // payment_orders：同上，先删 row_version 再加 version。
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "payment_orders");

            migrationBuilder.AddColumn<byte[]>(
                name: "version",
                table: "payment_orders",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }
    }
}
