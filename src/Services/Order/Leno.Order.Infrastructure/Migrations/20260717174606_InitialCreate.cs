using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "freight_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    free_shipping_threshold = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    seller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_freight_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "logistics_companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    service_phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    support_tracking = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logistics_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    order_type = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    seller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    items_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    points_offset_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    freight_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    recipient_name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    recipient_phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    province = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    city = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    district = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    address_detail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    payment_method = table.Column<int>(type: "int", nullable: true),
                    payment_initiated = table.Column<bool>(type: "bit", nullable: false),
                    payment_initiated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    expire_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    paid_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    payment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    trade_no = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    shipped_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    logistics_no = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LogisticsCompanyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    after_sales_window_ends_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cancel_reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
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
                name: "stock_reservation_compensations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    retry_count = table.Column<int>(type: "int", nullable: false),
                    max_retries = table.Column<int>(type: "int", nullable: false),
                    last_attempted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_error_message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservation_compensations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    base_line_qty = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_stock_reservations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "freight_region_rules",
                columns: table => new
                {
                    region_code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    first_unit = table.Column<int>(type: "int", nullable: false),
                    first_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    additional_unit = table.Column<int>(type: "int", nullable: false),
                    additional_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreightTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_freight_region_rules", x => x.region_code);
                    table.ForeignKey(
                        name: "FK_freight_region_rules_freight_templates_FreightTemplateId",
                        column: x => x.FreightTemplateId,
                        principalTable: "freight_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    product_sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    product_spu_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    product_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    product_sku_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    product_main_image = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    product_seller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    discount_allocation = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    source_cart_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_freight_region_rules_FreightTemplateId",
                table: "freight_region_rules",
                column: "FreightTemplateId");

            migrationBuilder.CreateIndex(
                name: "ix_freight_templates_seller_id",
                table: "freight_templates",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "ix_logistics_companies_code",
                table: "logistics_companies",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "ix_orders_order_no",
                table: "orders",
                column: "order_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_seller_id",
                table: "orders",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_orders_user_id",
                table: "orders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_stock_compensations_order_id",
                table: "stock_reservation_compensations",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_compensations_status",
                table: "stock_reservation_compensations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_sku_id",
                table: "stock_reservations",
                column: "sku_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "freight_region_rules");

            migrationBuilder.DropTable(
                name: "logistics_companies");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "stock_reservation_compensations");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropTable(
                name: "freight_templates");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
