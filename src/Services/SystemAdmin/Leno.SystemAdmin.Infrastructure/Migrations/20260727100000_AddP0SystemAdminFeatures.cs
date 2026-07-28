using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SystemAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// P0 系统管理功能迁移：创建 menus 与 login_logs 两张表。
    /// menus：树形菜单聚合，ParentId 自引用索引 + (Type, Visible) 复合索引；
    /// login_logs：仅追加登录日志，3 个降序索引（login_at / username+login_at / result+login_at）
    ///             + event_id 唯一索引（幂等去重，对应 Task 3.15 LoginLog.EventId）。
    /// </summary>
    public partial class AddP0SystemAdminFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. menus 表：树形菜单聚合根
            migrationBuilder.CreateTable(
                name: "menus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    parent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    type = table.Column<byte>(type: "tinyint", nullable: false),
                    path = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    component = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    sort = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    permission = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    roles = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    visible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    cache = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menus", x => x.id);
                });

            // 2. login_logs 表：仅追加登录日志聚合根
            migrationBuilder.CreateTable(
                name: "login_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    geo_location = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    browser = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    os = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    result = table.Column<byte>(type: "tinyint", nullable: false),
                    failure_reason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    duration_ms = table.Column<int>(type: "int", nullable: false),
                    user_agent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    device_fingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    referer_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    trace_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    login_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_logs", x => x.id);
                });

            // 3. menus 索引
            migrationBuilder.CreateIndex(
                name: "ix_menus_parent_id",
                table: "menus",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_menus_type_visible",
                table: "menus",
                columns: new[] { "type", "visible" });

            // 4. login_logs 索引（3 个降序 + 1 个唯一）
            migrationBuilder.CreateIndex(
                name: "ix_login_logs_login_at",
                table: "login_logs",
                column: "login_at");

            migrationBuilder.CreateIndex(
                name: "ix_login_logs_username_login_at",
                table: "login_logs",
                columns: new[] { "username", "login_at" });

            migrationBuilder.CreateIndex(
                name: "ix_login_logs_result_login_at",
                table: "login_logs",
                columns: new[] { "result", "login_at" });

            migrationBuilder.CreateIndex(
                name: "ix_login_logs_event_id",
                table: "login_logs",
                column: "event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_logs");

            migrationBuilder.DropTable(
                name: "menus");
        }
    }
}
