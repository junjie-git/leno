using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel20260909 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "cart_merge_records",
                columns: table => new
                {
                    anonymous_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    merged_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    merged_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_merge_records", x => x.anonymous_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages",
                columns: new[] { "shard_key", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_merge_records");

            migrationBuilder.DropIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "aggregate_root_id",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "shard_key",
                table: "outbox_messages");
        }
    }
}
