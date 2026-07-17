using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SystemAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    module = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    operator_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    operator_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    request_summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ip_address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    operator_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    resource_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    resource_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    request_summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    response_status = table.Column<int>(type: "int", nullable: false),
                    ip_address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                name: "dashboard_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    report_type = table.Column<int>(type: "int", nullable: false),
                    period_start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    period_end = table.Column<DateTime>(type: "datetime2", nullable: false),
                    metrics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    granularity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    data_version = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_dictionaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_dictionaries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dead_letter_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    original_message_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    source_context = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    original_topic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    error_reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    operator_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    discard_reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letter_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feature_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false),
                    strategy = table.Column<int>(type: "int", nullable: false),
                    rules = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "index_rebuild_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_context = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    index_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    triggered_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    progress = table.Column<int>(type: "int", nullable: false),
                    error_message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    retry_count = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_index_rebuild_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operation_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    operator_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    operation_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    module = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    before_snapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    after_snapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operators",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    role = table.Column<int>(type: "int", nullable: false),
                    permissions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operators", x => x.id);
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
                name: "rate_limit_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_api = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    target_context = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    limit = table.Column<int>(type: "int", nullable: false),
                    window_seconds = table.Column<int>(type: "int", nullable: false),
                    algorithm = table.Column<int>(type: "int", nullable: false),
                    scope = table.Column<int>(type: "int", nullable: false),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_limit_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    report_type = table.Column<int>(type: "int", nullable: false),
                    snapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    reconciled_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    alert_triggered = table.Column<bool>(type: "bit", nullable: false),
                    correction_triggered = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    job_type = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    cron_expression = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    last_run_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_run_status = table.Column<int>(type: "int", nullable: false),
                    next_run_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    target_audience = table.Column<int>(type: "int", nullable: false),
                    publish_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    expire_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_announcements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    group = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_encrypted = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dictionary_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    dictionary_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("PK_dictionary_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_dictionary_items_data_dictionaries_dictionary_id",
                        column: x => x.dictionary_id,
                        principalTable: "data_dictionaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_action",
                table: "audit_log_entries",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_event_id",
                table: "audit_log_entries",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_module",
                table: "audit_log_entries",
                column: "module");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_operator_id",
                table: "audit_log_entries",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_timestamp",
                table: "audit_log_entries",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_operator_id",
                table: "audit_logs",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_resource_type",
                table: "audit_logs",
                column: "resource_type");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_reports_type_generated_at",
                table: "dashboard_reports",
                columns: new[] { "report_type", "generated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_data_dictionaries_code",
                table: "data_dictionaries",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages",
                column: "original_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_messages_source_context",
                table: "dead_letter_messages",
                column: "source_context");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_messages_status",
                table: "dead_letter_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_items_dictionary_id",
                table: "dictionary_items",
                column: "dictionary_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_flags_key",
                table: "feature_flags",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_index_rebuild_tasks_context_index_status",
                table: "index_rebuild_tasks",
                columns: new[] { "target_context", "index_name", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_operation_logs_occurred_at",
                table: "operation_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_operation_logs_operator_id",
                table: "operation_logs",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "ix_operators_user_id",
                table: "operators",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status",
                table: "outbox_messages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_rate_limit_rules_enabled",
                table: "rate_limit_rules",
                column: "enabled");

            migrationBuilder.CreateIndex(
                name: "ix_rate_limit_rules_target_api",
                table: "rate_limit_rules",
                column: "target_api");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_records_type_reconciled_at",
                table: "reconciliation_records",
                columns: new[] { "report_type", "reconciled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_tasks_status",
                table: "scheduled_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_system_announcements_status",
                table: "system_announcements",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_system_configs_group",
                table: "system_configs",
                column: "group");

            migrationBuilder.CreateIndex(
                name: "ix_system_configs_key",
                table: "system_configs",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "dashboard_reports");

            migrationBuilder.DropTable(
                name: "dead_letter_messages");

            migrationBuilder.DropTable(
                name: "dictionary_items");

            migrationBuilder.DropTable(
                name: "feature_flags");

            migrationBuilder.DropTable(
                name: "index_rebuild_tasks");

            migrationBuilder.DropTable(
                name: "operation_logs");

            migrationBuilder.DropTable(
                name: "operators");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "rate_limit_rules");

            migrationBuilder.DropTable(
                name: "reconciliation_records");

            migrationBuilder.DropTable(
                name: "scheduled_tasks");

            migrationBuilder.DropTable(
                name: "system_announcements");

            migrationBuilder.DropTable(
                name: "system_configs");

            migrationBuilder.DropTable(
                name: "data_dictionaries");
        }
    }
}
