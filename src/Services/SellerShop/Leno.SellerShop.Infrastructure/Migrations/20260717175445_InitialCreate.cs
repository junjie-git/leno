using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SellerShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "seller_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    real_name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    id_card = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: true),
                    business_license_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    bank_account = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shop_dashboard_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shop_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    total_orders = table.Column<int>(type: "int", nullable: false),
                    pending_orders = table.Column<int>(type: "int", nullable: false),
                    completed_orders = table.Column<int>(type: "int", nullable: false),
                    total_revenue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_dashboard_data", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shop_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shop_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    order_count = table.Column<int>(type: "int", nullable: false),
                    sales_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    sales_currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    product_count = table.Column<int>(type: "int", nullable: false),
                    avg_rating = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    rating_sum = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    rating_count = table.Column<int>(type: "int", nullable: false),
                    refund_count = table.Column<int>(type: "int", nullable: false),
                    refund_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_metrics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    seller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shop_name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    logo = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    contact_phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    contact_email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    business_license_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    product_count = table.Column<int>(type: "int", nullable: false),
                    status_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shops", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shop_qualifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shop_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    image_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    valid_from = table.Column<DateTime>(type: "datetime2", nullable: false),
                    valid_to = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    reject_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_qualifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_shop_qualifications_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_seller_profiles_status",
                table: "seller_profiles",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_seller_profiles_user_id",
                table: "seller_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_dashboard_data_shop_id",
                table: "shop_dashboard_data",
                column: "shop_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_metrics_shop_date",
                table: "shop_metrics",
                columns: new[] { "shop_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_metrics_shop_id",
                table: "shop_metrics",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_qualifications_shop_id",
                table: "shop_qualifications",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_qualifications_shop_type",
                table: "shop_qualifications",
                columns: new[] { "shop_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_qualifications_status",
                table: "shop_qualifications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_shops_seller_id",
                table: "shops",
                column: "seller_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shops_status",
                table: "shops",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "seller_profiles");

            migrationBuilder.DropTable(
                name: "shop_dashboard_data");

            migrationBuilder.DropTable(
                name: "shop_metrics");

            migrationBuilder.DropTable(
                name: "shop_qualifications");

            migrationBuilder.DropTable(
                name: "shops");
        }
    }
}
