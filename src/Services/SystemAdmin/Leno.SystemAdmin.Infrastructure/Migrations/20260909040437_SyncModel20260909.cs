using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SystemAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel20260909 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages");

            migrationBuilder.AlterColumn<byte[]>(
                name: "version",
                table: "rate_limit_rules",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

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

            migrationBuilder.AddColumn<byte[]>(
                name: "version",
                table: "menus",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "version",
                table: "login_logs",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "audit_logs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "outbox_archive_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    context = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    archived_count = table.Column<int>(type: "int", nullable: false),
                    archived_before = table.Column<DateTime>(type: "datetime2", nullable: false),
                    archived_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    archived_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_archive_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages",
                columns: new[] { "shard_key", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages",
                column: "original_message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_id",
                table: "audit_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_archive_records_archived_at",
                table: "outbox_archive_records",
                column: "archived_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_archive_records_context",
                table: "outbox_archive_records",
                column: "context");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_archive_records");

            migrationBuilder.DropIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_tenant_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "aggregate_root_id",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "shard_key",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "version",
                table: "menus");

            migrationBuilder.DropColumn(
                name: "version",
                table: "login_logs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "audit_logs");

            migrationBuilder.AlterColumn<byte[]>(
                name: "version",
                table: "rate_limit_rules",
                type: "rowversion",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages",
                column: "original_message_id");
        }
    }
}
