using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 国际化预留扩展位（DG-8 决策门约束，仅预留不实际落地）：
    /// - notification_templates 表新增 culture 列（nvarchar(16)，nullable，null = zh-CN 默认行为不变）。
    /// - 重建 ix_notification_templates_code_channel 唯一索引，增加筛选条件 [culture] IS NULL，
    ///   限定仅对默认文化（null）行生效，为多语言变体（非 null culture）让出空间。
    /// - 新增 uq_notification_templates_code_channel_culture 复合唯一索引，筛选 [culture] IS NOT NULL，
    ///   DG-8 通过后同一 code+channel 可按 culture 维度创建多语言变体。当前阶段无非 null culture 数据，索引存在但不影响现有行为。
    /// </summary>
    public partial class AddNotificationTemplateCulture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 新增 culture 列（nullable，null = zh-CN 默认行为不变）。
            migrationBuilder.AddColumn<string>(
                name: "culture",
                table: "notification_templates",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            // 2. 重建 ix_notification_templates_code_channel 唯一索引，增加筛选条件 [culture] IS NULL。
            //    原索引无筛选条件（全表唯一），重建后仅对默认文化（null）行生效，
            //    允许同一 code+channel 存在一个 null culture 行 + 多个非 null culture 多语言变体行。
            //    现有数据 culture 均为 null，且原索引已保证 (code, channel) 唯一，重建无冲突。
            migrationBuilder.DropIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates",
                columns: new[] { "code", "channel" },
                unique: true,
                filter: "[culture] IS NULL");

            // 3. 新增 uq_notification_templates_code_channel_culture 复合唯一索引，筛选 [culture] IS NOT NULL。
            //    国际化预留扩展位：DG-8 通过后，同一 code+channel 可按 culture 维度创建多语言变体（zh-CN / en-US 等）。
            //    当前阶段无非 null culture 数据，索引存在但不影响现有行为。
            migrationBuilder.CreateIndex(
                name: "uq_notification_templates_code_channel_culture",
                table: "notification_templates",
                columns: new[] { "code", "channel", "culture" },
                unique: true,
                filter: "[culture] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：先删多语言变体唯一索引，再删 culture 列，最后重建原无筛选唯一索引。
            migrationBuilder.DropIndex(
                name: "uq_notification_templates_code_channel_culture",
                table: "notification_templates");

            migrationBuilder.DropIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates");

            migrationBuilder.DropColumn(
                name: "culture",
                table: "notification_templates");

            migrationBuilder.CreateIndex(
                name: "ix_notification_templates_code_channel",
                table: "notification_templates",
                columns: new[] { "code", "channel" },
                unique: true);
        }
    }
}
