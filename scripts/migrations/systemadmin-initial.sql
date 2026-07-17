IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [audit_log_entries] (
        [id] uniqueidentifier NOT NULL,
        [event_id] uniqueidentifier NOT NULL,
        [event_type] nvarchar(128) NOT NULL,
        [aggregate_id] uniqueidentifier NOT NULL,
        [module] nvarchar(64) NOT NULL,
        [action] nvarchar(128) NOT NULL,
        [operator_id] uniqueidentifier NOT NULL,
        [operator_name] nvarchar(128) NULL,
        [request_summary] nvarchar(2000) NULL,
        [timestamp] datetime2 NOT NULL,
        [ip_address] nvarchar(64) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_audit_log_entries] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [audit_logs] (
        [id] uniqueidentifier NOT NULL,
        [operator_id] uniqueidentifier NOT NULL,
        [action] nvarchar(128) NOT NULL,
        [resource_type] nvarchar(64) NOT NULL,
        [resource_id] nvarchar(64) NOT NULL,
        [request_summary] nvarchar(2000) NULL,
        [response_status] int NOT NULL,
        [ip_address] nvarchar(64) NULL,
        [trace_id] nvarchar(64) NULL,
        [occurred_at] datetime2 NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_audit_logs] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [dashboard_reports] (
        [id] uniqueidentifier NOT NULL,
        [report_type] int NOT NULL,
        [period_start] datetime2 NOT NULL,
        [period_end] datetime2 NOT NULL,
        [metrics] nvarchar(max) NOT NULL,
        [granularity] nvarchar(16) NOT NULL,
        [generated_at] datetime2 NOT NULL,
        [data_version] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_dashboard_reports] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [data_dictionaries] (
        [id] uniqueidentifier NOT NULL,
        [code] nvarchar(64) NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [description] nvarchar(500) NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_data_dictionaries] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [dead_letter_messages] (
        [id] uniqueidentifier NOT NULL,
        [original_message_id] nvarchar(128) NOT NULL,
        [source_context] nvarchar(256) NOT NULL,
        [original_topic] nvarchar(256) NOT NULL,
        [payload] nvarchar(max) NOT NULL,
        [headers] nvarchar(max) NOT NULL,
        [error_reason] nvarchar(max) NOT NULL,
        [status] int NOT NULL,
        [operator_id] nvarchar(64) NULL,
        [discard_reason] nvarchar(1000) NULL,
        [occurred_at] datetime2 NOT NULL,
        [processed_at] datetime2 NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_dead_letter_messages] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [feature_flags] (
        [id] uniqueidentifier NOT NULL,
        [key] nvarchar(128) NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [description] nvarchar(500) NULL,
        [is_enabled] bit NOT NULL,
        [strategy] int NOT NULL,
        [rules] nvarchar(max) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_feature_flags] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [index_rebuild_tasks] (
        [id] uniqueidentifier NOT NULL,
        [target_context] nvarchar(128) NOT NULL,
        [index_name] nvarchar(256) NOT NULL,
        [status] int NOT NULL,
        [triggered_by] nvarchar(64) NOT NULL,
        [progress] int NOT NULL,
        [error_message] nvarchar(2000) NULL,
        [retry_count] int NOT NULL,
        [created_at] datetime2 NOT NULL,
        [started_at] datetime2 NULL,
        [completed_at] datetime2 NULL,
        [version] rowversion NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_index_rebuild_tasks] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [operation_logs] (
        [id] uniqueidentifier NOT NULL,
        [operator_id] uniqueidentifier NOT NULL,
        [operation_type] nvarchar(64) NOT NULL,
        [module] nvarchar(64) NOT NULL,
        [description] nvarchar(500) NULL,
        [before_snapshot] nvarchar(4000) NULL,
        [after_snapshot] nvarchar(4000) NULL,
        [ip_address] nvarchar(64) NULL,
        [occurred_at] datetime2 NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_operation_logs] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [operators] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [display_name] nvarchar(100) NOT NULL,
        [role] int NOT NULL,
        [permissions] nvarchar(max) NOT NULL,
        [status] int NOT NULL,
        [last_login_at] datetime2 NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_operators] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [outbox_messages] (
        [id] uniqueidentifier NOT NULL,
        [type] nvarchar(512) NOT NULL,
        [payload] nvarchar(max) NOT NULL,
        [occurred_at] datetime2 NOT NULL,
        [processed_at] datetime2 NULL,
        [publishing_started_at] datetime2 NULL,
        [retry_count] int NOT NULL,
        [error] nvarchar(max) NULL,
        [status] int NOT NULL,
        CONSTRAINT [PK_outbox_messages] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [rate_limit_rules] (
        [id] uniqueidentifier NOT NULL,
        [target_api] nvarchar(256) NOT NULL,
        [target_context] nvarchar(64) NULL,
        [limit] int NOT NULL,
        [window_seconds] int NOT NULL,
        [algorithm] int NOT NULL,
        [scope] int NOT NULL,
        [enabled] bit NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_rate_limit_rules] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [reconciliation_records] (
        [id] uniqueidentifier NOT NULL,
        [report_type] int NOT NULL,
        [snapshot] nvarchar(max) NOT NULL,
        [reconciled_at] datetime2 NOT NULL,
        [status] int NOT NULL,
        [alert_triggered] bit NOT NULL,
        [correction_triggered] bit NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_reconciliation_records] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [scheduled_tasks] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [job_type] nvarchar(256) NOT NULL,
        [cron_expression] nvarchar(128) NOT NULL,
        [parameters] nvarchar(max) NULL,
        [status] int NOT NULL,
        [last_run_at] datetime2 NULL,
        [last_run_status] int NOT NULL,
        [next_run_at] datetime2 NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_scheduled_tasks] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [system_announcements] (
        [id] uniqueidentifier NOT NULL,
        [title] nvarchar(200) NOT NULL,
        [content] nvarchar(max) NOT NULL,
        [type] int NOT NULL,
        [target_audience] int NOT NULL,
        [publish_at] datetime2 NULL,
        [expire_at] datetime2 NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_system_announcements] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [system_configs] (
        [id] uniqueidentifier NOT NULL,
        [key] nvarchar(128) NOT NULL,
        [value] nvarchar(max) NOT NULL,
        [group] nvarchar(64) NOT NULL,
        [description] nvarchar(500) NULL,
        [is_encrypted] bit NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_system_configs] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE TABLE [dictionary_items] (
        [id] uniqueidentifier NOT NULL,
        [dictionary_id] uniqueidentifier NOT NULL,
        [code] nvarchar(64) NOT NULL,
        [label] nvarchar(128) NOT NULL,
        [value] nvarchar(256) NOT NULL,
        [sort_order] int NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_dictionary_items] PRIMARY KEY ([id]),
        CONSTRAINT [FK_dictionary_items_data_dictionaries_dictionary_id] FOREIGN KEY ([dictionary_id]) REFERENCES [data_dictionaries] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_log_entries_action] ON [audit_log_entries] ([action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_audit_log_entries_event_id] ON [audit_log_entries] ([event_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_log_entries_module] ON [audit_log_entries] ([module]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_log_entries_operator_id] ON [audit_log_entries] ([operator_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_log_entries_timestamp] ON [audit_log_entries] ([timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_logs_occurred_at] ON [audit_logs] ([occurred_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_logs_operator_id] ON [audit_logs] ([operator_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_logs_resource_type] ON [audit_logs] ([resource_type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_dashboard_reports_type_generated_at] ON [dashboard_reports] ([report_type], [generated_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_data_dictionaries_code] ON [data_dictionaries] ([code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_dead_letter_messages_original_message_id] ON [dead_letter_messages] ([original_message_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_dead_letter_messages_source_context] ON [dead_letter_messages] ([source_context]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_dead_letter_messages_status] ON [dead_letter_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_dictionary_items_dictionary_id] ON [dictionary_items] ([dictionary_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_feature_flags_key] ON [feature_flags] ([key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_index_rebuild_tasks_context_index_status] ON [index_rebuild_tasks] ([target_context], [index_name], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_operation_logs_occurred_at] ON [operation_logs] ([occurred_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_operation_logs_operator_id] ON [operation_logs] ([operator_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_operators_user_id] ON [operators] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_rate_limit_rules_enabled] ON [rate_limit_rules] ([enabled]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_rate_limit_rules_target_api] ON [rate_limit_rules] ([target_api]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_reconciliation_records_type_reconciled_at] ON [reconciliation_records] ([report_type], [reconciled_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_scheduled_tasks_status] ON [scheduled_tasks] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_system_announcements_status] ON [system_announcements] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_system_configs_group] ON [system_configs] ([group]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_system_configs_key] ON [system_configs] ([key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175558_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717175558_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

