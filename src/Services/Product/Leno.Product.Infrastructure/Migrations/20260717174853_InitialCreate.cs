using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Product.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    logo = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    parent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    level = table.Column<int>(type: "int", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
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

            migrationBuilder.CreateTable(
                name: "spus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shop_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    seller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    subtitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    main_image_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    brand_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    specs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    suspended_by_shop = table.Column<bool>(type: "bit", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    average_score = table.Column<double>(type: "float", nullable: false),
                    review_count = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    audit_history = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    price_change_history = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    stock_operation_history = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_baselines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    available_qty = table.Column<int>(type: "int", nullable: false),
                    reserved_qty = table.Column<int>(type: "int", nullable: false),
                    deducted_qty = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_baselines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    spu_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    stock_qty = table.Column<int>(type: "int", nullable: false),
                    spec_attributes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    image_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skus", x => x.id);
                    table.ForeignKey(
                        name: "FK_skus_spus_spu_id",
                        column: x => x.spu_id,
                        principalTable: "spus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spu_images",
                columns: table => new
                {
                    SPUId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    is_main = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spu_images", x => new { x.SPUId, x.Id });
                    table.ForeignKey(
                        name: "FK_spu_images_spus_SPUId",
                        column: x => x.SPUId,
                        principalTable: "spus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_brands_name",
                table: "brands",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_skus_sku_code",
                table: "skus",
                column: "sku_code");

            migrationBuilder.CreateIndex(
                name: "ix_skus_spu_id",
                table: "skus",
                column: "spu_id");

            migrationBuilder.CreateIndex(
                name: "ix_spus_category_id",
                table: "spus",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_spus_shop_id",
                table: "spus",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "ix_spus_status",
                table: "spus",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_stock_baselines_sku_id",
                table: "stock_baselines",
                column: "sku_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brands");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "skus");

            migrationBuilder.DropTable(
                name: "spu_images");

            migrationBuilder.DropTable(
                name: "stock_baselines");

            migrationBuilder.DropTable(
                name: "spus");
        }
    }
}
