using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SellerShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "export_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shop_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    seller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    report_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    record_count = table.Column<int>(type: "int", nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    file_path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    error_message = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_export_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_export_tasks_shop_id_status",
                table: "export_tasks",
                columns: new[] { "shop_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_export_tasks_status_created_at",
                table: "export_tasks",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "export_tasks");
        }
    }
}
