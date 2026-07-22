using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.ReviewAfterSales.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 补齐 Review 卖家回复相关字段（seller_id/seller_reply_by/seller_reply_at）
    /// 与 AfterSales 审核驳回/确认收货审计字段（rejected_at/return_confirmed_by）。
    /// 这些字段已在领域实体与 Configuration 中配置，但缺失对应数据库迁移。
    /// </summary>
    public partial class AddReviewSellerFieldsAndAfterSalesAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // reviews 表：补充卖家标识与卖家回复审计字段。
            // seller_id 为非空 Guid，使用 Guid.Empty 作为历史数据默认值，
            // 与领域层 SellerId 非空语义对齐（新数据由工厂方法强制非空校验）。
            migrationBuilder.AddColumn<Guid>(
                name: "seller_id",
                table: "reviews",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            // seller_reply_by：卖家回复操作人标识，回复前为空。
            migrationBuilder.AddColumn<Guid>(
                name: "seller_reply_by",
                table: "reviews",
                type: "uniqueidentifier",
                nullable: true);

            // seller_reply_at：卖家回复时间（UTC），回复前为空。
            migrationBuilder.AddColumn<DateTime>(
                name: "seller_reply_at",
                table: "reviews",
                type: "datetime2",
                nullable: true);

            // after_sales 表：补充审核驳回时间字段，与 ApprovedAt 互斥以区分审核语义。
            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                table: "after_sales",
                type: "datetime2",
                nullable: true);

            // after_sales 表：补充卖家确认收货操作人标识，用于审计追溯。
            migrationBuilder.AddColumn<Guid>(
                name: "return_confirmed_by",
                table: "after_sales",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向回滚：按 Up 中添加的逆序删除列。
            migrationBuilder.DropColumn(
                name: "return_confirmed_by",
                table: "after_sales");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "after_sales");

            migrationBuilder.DropColumn(
                name: "seller_reply_at",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "seller_reply_by",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "seller_id",
                table: "reviews");
        }
    }
}
