using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.UserAuth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel20260909 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_phone_number",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_addresses_user_default",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "version",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "users",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

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
                name: "browse_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    spu_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    viewed_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_browse_histories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "favorites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    spu_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    favorited_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_favorites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dnd_enabled = table.Column<bool>(type: "bit", nullable: false),
                    dnd_start = table.Column<TimeSpan>(type: "time", nullable: true),
                    dnd_end = table.Column<TimeSpan>(type: "time", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preference_items",
                columns: table => new
                {
                    event_type = table.Column<int>(type: "int", nullable: false),
                    notification_preferences_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    in_app_enabled = table.Column<bool>(type: "bit", nullable: false),
                    sms_enabled = table.Column<bool>(type: "bit", nullable: false),
                    email_enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preference_items", x => new { x.notification_preferences_id, x.event_type });
                    table.ForeignKey(
                        name: "FK_notification_preference_items_notification_preferences_notification_preferences_id",
                        column: x => x.notification_preferences_id,
                        principalTable: "notification_preferences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true,
                filter: "[email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true,
                filter: "[phone_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages",
                columns: new[] { "shard_key", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_addresses_user_default",
                table: "addresses",
                columns: new[] { "user_id", "is_default" },
                unique: true,
                filter: "[is_default] = 1");

            migrationBuilder.CreateIndex(
                name: "ix_browse_histories_user_id",
                table: "browse_histories",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_browse_histories_user_spu_viewed_at",
                table: "browse_histories",
                columns: new[] { "user_id", "spu_id", "viewed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_favorites_user_id",
                table: "favorites",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_favorites_user_spu",
                table: "favorites",
                columns: new[] { "user_id", "spu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_user_id",
                table: "notification_preferences",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "browse_histories");

            migrationBuilder.DropTable(
                name: "favorites");

            migrationBuilder.DropTable(
                name: "notification_preference_items");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropIndex(
                name: "ix_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_phone_number",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_addresses_user_default",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "aggregate_root_id",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "shard_key",
                table: "outbox_messages");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "version",
                table: "users",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true,
                filter: "\"email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true,
                filter: "\"phone_number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_addresses_user_default",
                table: "addresses",
                columns: new[] { "user_id", "is_default" });
        }
    }
}
