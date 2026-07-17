using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.UserAuth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    recipient_name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    recipient_phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    province = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    city = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    district = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    detail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    tag = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    is_default = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addresses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    operator_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    resource_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    resource_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    before_snapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    after_snapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    operated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    trace_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oauth_clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    client_id = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    client_secret = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    redirect_uri = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_clients", x => x.id);
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
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    permissions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    is_built_in = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    username = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    phone_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    password_hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    nickname = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    avatar_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    default_address_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    failed_login_count = table.Column<int>(type: "int", nullable: false),
                    locked_until = table.Column<DateTime>(type: "datetime2", nullable: true),
                    two_factor_enabled = table.Column<bool>(type: "bit", nullable: false),
                    two_factor_secret = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_external_logins",
                columns: table => new
                {
                    provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    provider_user_id = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    avatar_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    linked_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_external_logins", x => new { x.user_id, x.provider });
                    table.ForeignKey(
                        name: "FK_user_external_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    role_type = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_type });
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_addresses_user_default",
                table: "addresses",
                columns: new[] { "user_id", "is_default" });

            migrationBuilder.CreateIndex(
                name: "ix_addresses_user_id",
                table: "addresses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_operated_at",
                table: "audit_logs",
                column: "operated_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_operator_id",
                table: "audit_logs",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_clients_provider",
                table: "oauth_clients",
                column: "provider",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_external_logins_provider_user_id",
                table: "user_external_logins",
                columns: new[] { "provider", "provider_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true,
                filter: "\"email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true,
                filter: "\"phone_number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "addresses");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "oauth_clients");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "user_external_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
