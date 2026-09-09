using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.ReviewAfterSales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel20260909 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "append_content",
                table: "reviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "append_images",
                table: "reviews",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "appended_at",
                table: "reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "aggregate_root_id",
                table: "outbox_messages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
                name: "append_content",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "append_images",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "appended_at",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "aggregate_root_id",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "shard_key",
                table: "outbox_messages");
        }
    }
}
