using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 补齐 Notification BC 缺失的索引与新增 NotificationConfig 表：
    /// 1. notification_templates (code, channel) 唯一索引（原为非唯一）；
    /// 2. notification_records idempotency_key 唯一过滤索引（原为非唯一）；
    /// 3. notification_records channel_message_id 索引（新增）；
    /// 4. notification_records (status, next_retry_at) 复合索引（新增）；
    /// 5. notification_records (status, retry_count) 复合索引（新增）；
    /// 6. notification_configs 表与 (channel, config_key) 唯一索引（新增聚合）。
    /// </summary>
    public partial class AddNotificationIndexesAndConfigTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 重建 notification_templates (code, channel) 唯一索引
            migrationBuilder.DropIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates",
                columns: new[] { "code", "channel" },
                unique: true);

            // 2. 重建 notification_records idempotency_key 唯一过滤索引
            migrationBuilder.DropIndex(
                name: "ix_notification_records_idempotency_key",
                table: "notification_records");

            migrationBuilder.CreateIndex(
                name: "ix_notification_records_idempotency_key",
                table: "notification_records",
                column: "idempotency_key",
                unique: true,
                filter: "[idempotency_key] IS NOT NULL");

            // 3. 新建 notification_records channel_message_id 索引
            migrationBuilder.CreateIndex(
                name: "ix_notification_records_channel_message_id",
                table: "notification_records",
                column: "channel_message_id");

            // 4. 新建 notification_records (status, next_retry_at) 复合索引
            migrationBuilder.CreateIndex(
                name: "ix_notification_records_status_next_retry_at",
                table: "notification_records",
                columns: new[] { "status", "next_retry_at" });

            // 5. 新建 notification_records (status, retry_count) 复合索引
            migrationBuilder.CreateIndex(
                name: "ix_notification_records_status_retry_count",
                table: "notification_records",
                columns: new[] { "status", "retry_count" });

            // 6. 新建 notification_configs 表
            migrationBuilder.CreateTable(
                name: "notification_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    channel = table.Column<int>(type: "int", nullable: false),
                    config_key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    config_value = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    is_sensitive = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_configs", x => x.id);
                });

            // (channel, config_key) 唯一索引
            migrationBuilder.CreateIndex(
                name: "ix_notification_configs_channel_key",
                table: "notification_configs",
                columns: new[] { "channel", "config_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 删除 notification_configs 表（含索引）
            migrationBuilder.DropTable(
                name: "notification_configs");

            // 删除新增的 notification_records 复合索引
            migrationBuilder.DropIndex(
                name: "ix_notification_records_status_retry_count",
                table: "notification_records");

            migrationBuilder.DropIndex(
                name: "ix_notification_records_status_next_retry_at",
                table: "notification_records");

            // 删除新增的 notification_records channel_message_id 索引
            migrationBuilder.DropIndex(
                name: "ix_notification_records_channel_message_id",
                table: "notification_records");

            // 回滚 idempotency_key 为非唯一、无过滤的普通索引
            migrationBuilder.DropIndex(
                name: "ix_notification_records_idempotency_key",
                table: "notification_records");

            migrationBuilder.CreateIndex(
                name: "ix_notification_records_idempotency_key",
                table: "notification_records",
                column: "idempotency_key");

            // 回滚 (code, channel) 为非唯一索引
            migrationBuilder.DropIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates",
                columns: new[] { "code", "channel" });
        }
    }
}
