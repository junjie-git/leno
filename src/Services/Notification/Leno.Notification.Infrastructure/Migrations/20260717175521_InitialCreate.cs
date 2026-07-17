using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_channels = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
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
                name: "notification_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    template_code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    channel = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    retry_count = table.Column<int>(type: "int", nullable: false),
                    max_retry = table.Column<int>(type: "int", nullable: false),
                    next_retry_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_read = table.Column<bool>(type: "bit", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    failed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    error_message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    error_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    content_snapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    channel_message_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    channel_receipt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    business_ref = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    idempotency_key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    channel = table.Column<int>(type: "int", nullable: false),
                    subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    sms_template_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    operator_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    variables = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    publishing_started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    retry_count = table.Column<int>(type: "int", nullable: false),
                    error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_user_id",
                table: "notification_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_records_event_id",
                table: "notification_records",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_records_idempotency_key",
                table: "notification_records",
                column: "idempotency_key");

            migrationBuilder.CreateIndex(
                name: "ix_notification_records_status",
                table: "notification_records",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_notification_records_template_code",
                table: "notification_records",
                column: "template_code");

            migrationBuilder.CreateIndex(
                name: "ix_notification_records_user_id",
                table: "notification_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates",
                columns: new[] { "code", "channel" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notification_records");

            migrationBuilder.DropTable(
                name: "notification_templates");

            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
