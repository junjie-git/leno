using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Promotion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeckillActivityName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SeckillPreOccupationRecords",
                table: "SeckillPreOccupationRecords");

            migrationBuilder.RenameTable(
                name: "SeckillPreOccupationRecords",
                newName: "seckill_pre_occupation_records");

            migrationBuilder.RenameIndex(
                name: "IX_SeckillPreOccupationRecords_OrderId",
                table: "seckill_pre_occupation_records",
                newName: "IX_seckill_pre_occupation_records_OrderId");

            migrationBuilder.RenameIndex(
                name: "IX_SeckillPreOccupationRecords_IsFulfilled_IsRolledBack_PreOccupiedAt",
                table: "seckill_pre_occupation_records",
                newName: "IX_seckill_pre_occupation_records_IsFulfilled_IsRolledBack_PreOccupiedAt");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "seckill_activities",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddPrimaryKey(
                name: "PK_seckill_pre_occupation_records",
                table: "seckill_pre_occupation_records",
                column: "Id");

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

            migrationBuilder.DropPrimaryKey(
                name: "PK_seckill_pre_occupation_records",
                table: "seckill_pre_occupation_records");

            migrationBuilder.DropColumn(
                name: "name",
                table: "seckill_activities");

            migrationBuilder.DropColumn(
                name: "aggregate_root_id",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "shard_key",
                table: "outbox_messages");

            migrationBuilder.RenameTable(
                name: "seckill_pre_occupation_records",
                newName: "SeckillPreOccupationRecords");

            migrationBuilder.RenameIndex(
                name: "IX_seckill_pre_occupation_records_OrderId",
                table: "SeckillPreOccupationRecords",
                newName: "IX_SeckillPreOccupationRecords_OrderId");

            migrationBuilder.RenameIndex(
                name: "IX_seckill_pre_occupation_records_IsFulfilled_IsRolledBack_PreOccupiedAt",
                table: "SeckillPreOccupationRecords",
                newName: "IX_SeckillPreOccupationRecords_IsFulfilled_IsRolledBack_PreOccupiedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SeckillPreOccupationRecords",
                table: "SeckillPreOccupationRecords",
                column: "Id");
        }
    }
}
