using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.PointsMembership.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel20260909 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "version",
                table: "user_memberships",
                newName: "row_version");

            migrationBuilder.AddColumn<Guid>(
                name: "PointsAccountId",
                table: "points_ledgers",
                type: "uniqueidentifier",
                nullable: true);

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
                name: "points_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    action_type = table.Column<int>(type: "int", nullable: false),
                    points = table.Column<int>(type: "int", nullable: false),
                    daily_limit = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_points_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_points_ledgers_PointsAccountId",
                table: "points_ledgers",
                column: "PointsAccountId");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages",
                columns: new[] { "shard_key", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_points_rules_code",
                table: "points_rules",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_points_rules_status",
                table: "points_rules",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_points_ledgers_points_accounts_PointsAccountId",
                table: "points_ledgers",
                column: "PointsAccountId",
                principalTable: "points_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_points_ledgers_points_accounts_PointsAccountId",
                table: "points_ledgers");

            migrationBuilder.DropTable(
                name: "points_rules");

            migrationBuilder.DropIndex(
                name: "IX_points_ledgers_PointsAccountId",
                table: "points_ledgers");

            migrationBuilder.DropIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "PointsAccountId",
                table: "points_ledgers");

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
                name: "row_version",
                table: "user_memberships",
                newName: "version");
        }
    }
}
