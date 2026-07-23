using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Promotion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionRuleDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "promotion_rule_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rule_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    priority = table.Column<int>(type: "int", nullable: false),
                    stacking = table.Column<int>(type: "int", nullable: false),
                    definition_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    // 业务版本号列名 definition_version，避免与 BaseDbContext 自动注入的 rowversion shadow property "version" 冲突
                    definition_version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    remark = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    // rowversion shadow property "version"，由 BaseDbContext.OnModelCreating 自动注入
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_rule_definitions", x => x.id);
                });

            // 同 RuleType 启用规则唯一索引，配合 [enabled]=1 过滤确保一种规则类型至多一条启用定义
            migrationBuilder.CreateIndex(
                name: "ux_promotion_rule_definitions_rule_type_enabled",
                table: "promotion_rule_definitions",
                columns: new[] { "rule_type", "enabled" },
                unique: true,
                filter: "[enabled] = 1");

            migrationBuilder.CreateIndex(
                name: "ix_promotion_rule_definitions_priority",
                table: "promotion_rule_definitions",
                column: "priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_promotion_rule_definitions_rule_type_enabled",
                table: "promotion_rule_definitions");

            migrationBuilder.DropIndex(
                name: "ix_promotion_rule_definitions_priority",
                table: "promotion_rule_definitions");

            migrationBuilder.DropTable(
                name: "promotion_rule_definitions");
        }
    }
}
