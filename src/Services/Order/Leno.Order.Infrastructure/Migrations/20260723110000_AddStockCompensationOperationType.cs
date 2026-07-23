using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockCompensationOperationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 新增 operation_type 列，区分补偿操作类型（NEW-P0-3）：
            // 0 = Release（释放预占，对应 IInventoryRepository.ReleaseAsync）
            // 1 = ReturnDeducted（归还已扣减，对应 IInventoryRepository.ReturnDeductedAsync）
            // 默认值 0（Release）保证历史 Pending 记录重试时仍调用 ReleaseAsync，行为与升级前一致。
            migrationBuilder.AddColumn<int>(
                name: "operation_type",
                table: "stock_reservation_compensations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "operation_type",
                table: "stock_reservation_compensations");
        }
    }
}
