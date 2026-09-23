using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.Review.Infrastructure.Migrations
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
                    status = table.Column<int>(type: "int", nullable: false),
                    schema_version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    aggregate_root_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    shard_key = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    spu_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sku_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    seller_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rating = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    seller_reply_content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    seller_reply_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    seller_reply_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    audited_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    auditor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    hidden_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    hidden_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    hide_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    append_content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    appended_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    append_images = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    images = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_shard_status",
                table: "outbox_messages",
                columns: new[] { "shard_key", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_order_line_id",
                table: "reviews",
                column: "order_line_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_seller_id",
                table: "reviews",
                column: "seller_id")
                .Annotation("SqlServer:Include", new[] { "created_at", "rating" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_spu_id",
                table: "reviews",
                column: "spu_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user_id",
                table: "reviews",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "reviews");
        }
    }
}
