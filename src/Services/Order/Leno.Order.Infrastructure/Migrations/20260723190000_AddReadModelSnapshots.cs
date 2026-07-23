using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 3.12：新增 read_model_snapshots 表，存储 CQRS 读模型快照以支持快照重建与增量回放。
    /// 主键 (aggregate_id, version)，state_json 以 JSON 文本存储读模型完整视图。
    /// </summary>
    public partial class AddReadModelSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "read_model_snapshots",
                columns: table => new
                {
                    aggregate_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    aggregate_type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    state_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    taken_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_read_model_snapshots", x => new { x.aggregate_id, x.version });
                });

            migrationBuilder.CreateIndex(
                name: "ix_read_model_snapshots_aggregate_type",
                table: "read_model_snapshots",
                column: "aggregate_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "read_model_snapshots");
        }
    }
}
