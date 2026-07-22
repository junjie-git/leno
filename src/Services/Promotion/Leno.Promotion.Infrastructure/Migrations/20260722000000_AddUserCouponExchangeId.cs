using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Promotion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCouponExchangeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "exchange_id",
                table: "user_coupons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_user_coupons_exchange_id",
                table: "user_coupons",
                column: "exchange_id",
                unique: true,
                filter: "[exchange_id] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_user_coupons_exchange_id",
                table: "user_coupons");

            migrationBuilder.DropColumn(
                name: "exchange_id",
                table: "user_coupons");
        }
    }
}
