using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 补充 notification_records (user_id, is_read, channel) 复合索引，
    /// 含 Include 列 created_at/template_code，使用 SQL Server ONLINE=ON 在线创建避免锁表，FILLFACTOR=90 预留页空间。
    /// 同时顺带补齐 outbox_messages.schema_version 字段与 notification_rate_limit_configs 表
    /// （ModelSnapshot 与数据库预存不一致，由阶段一实体配置变更未生成迁移遗留）。
    /// </summary>
    public partial class AddIxNotificationRecordsUserIsreadChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // outbox_messages 表：补齐 schema_version 字段（默认值 1，对应初始 schema 版本）。
            migrationBuilder.AddColumn<int>(
                name: "schema_version",
                table: "outbox_messages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // notification_rate_limit_configs 表：按渠道维度的限流配置聚合（预存实体配置补迁移）。
            migrationBuilder.CreateTable(
                name: "notification_rate_limit_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    channel = table.Column<int>(type: "int", nullable: false),
                    hourly_limit = table.Column<int>(type: "int", nullable: false),
                    daily_limit = table.Column<int>(type: "int", nullable: true),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_rate_limit_configs", x => x.id);
                });

            // notification_rate_limit_configs 表：channel 唯一索引（新表无数据，无需 ONLINE=ON）。
            migrationBuilder.CreateIndex(
                name: "ix_notification_rate_limit_configs_channel",
                table: "notification_rate_limit_configs",
                column: "channel",
                unique: true);

            // notification_records 表：在线创建 (user_id, is_read, channel) 复合索引，
            // INCLUDE created_at/template_code 形成覆盖索引，避免用户通知中心列表查询回表聚簇索引。
            // 列顺序：user_id（高选择性）在前，is_read/channel（等值过滤）紧随，符合最左前缀原则。
            // WITH (ONLINE = ON) 在线创建避免锁表（SQL Server Enterprise 版支持）；
            // FILLFACTOR = 90 预留 10% 页空间，减少高频写入场景的页分裂。
            migrationBuilder.Sql(
                @"CREATE INDEX ix_notification_records_user_isread_channel
                  ON notification_records (user_id, is_read, channel)
                  INCLUDE (created_at, template_code)
                  WITH (ONLINE = ON, FILLFACTOR = 90);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：先删索引，再删表，最后删列，避免依赖冲突。
            migrationBuilder.Sql(
                "DROP INDEX ix_notification_records_user_isread_channel ON notification_records;");

            migrationBuilder.DropTable(
                name: "notification_rate_limit_configs");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_messages");
        }
    }
}
