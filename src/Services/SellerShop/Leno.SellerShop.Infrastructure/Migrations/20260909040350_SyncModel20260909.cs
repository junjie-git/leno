using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SellerShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel20260909 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "shop_dashboard_data",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "shop_dashboard_data",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "shop_dashboard_data",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "shop_dashboard_data",
                newName: "created_at");

            migrationBuilder.AlterColumn<string>(
                name: "updated_by",
                table: "shop_dashboard_data",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "shop_dashboard_data",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cancelled_orders",
                table: "shop_dashboard_data",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "confirmed_orders",
                table: "shop_dashboard_data",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_amount",
                table: "shop_dashboard_data",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "aggregate_root_id",
                table: "outbox_messages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "schema_version",
                table: "outbox_messages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "shard_key",
                table: "outbox_messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages",
                columns: new[] { "shard_key", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "cancelled_orders",
                table: "shop_dashboard_data");

            migrationBuilder.DropColumn(
                name: "confirmed_orders",
                table: "shop_dashboard_data");

            migrationBuilder.DropColumn(
                name: "refunded_amount",
                table: "shop_dashboard_data");

            migrationBuilder.DropColumn(
                name: "aggregate_root_id",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "shard_key",
                table: "outbox_messages");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "shop_dashboard_data",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "shop_dashboard_data",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "shop_dashboard_data",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "shop_dashboard_data",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "shop_dashboard_data",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "shop_dashboard_data",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
