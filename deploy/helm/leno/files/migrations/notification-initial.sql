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
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE TABLE [notification_preferences] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [event_channels] nvarchar(max) NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_notification_preferences] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE TABLE [notification_records] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [template_code] nvarchar(128) NOT NULL,
        [event_id] uniqueidentifier NULL,
        [channel] int NOT NULL,
        [title] nvarchar(200) NOT NULL,
        [content] nvarchar(2000) NOT NULL,
        [status] int NOT NULL,
        [retry_count] int NOT NULL,
        [max_retry] int NOT NULL,
        [next_retry_at] datetime2 NULL,
        [is_read] bit NOT NULL,
        [sent_at] datetime2 NULL,
        [failed_at] datetime2 NULL,
        [error_message] nvarchar(500) NULL,
        [error_code] nvarchar(64) NULL,
        [content_snapshot] nvarchar(max) NULL,
        [channel_message_id] nvarchar(128) NULL,
        [channel_receipt] nvarchar(max) NULL,
        [business_ref] nvarchar(128) NULL,
        [idempotency_key] nvarchar(128) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_notification_records] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE TABLE [notification_templates] (
        [id] uniqueidentifier NOT NULL,
        [code] nvarchar(128) NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [channel] int NOT NULL,
        [subject] nvarchar(200) NOT NULL,
        [body] nvarchar(2000) NOT NULL,
        [sms_template_code] nvarchar(64) NULL,
        [description] nvarchar(512) NULL,
        [operator_id] uniqueidentifier NULL,
        [variables] nvarchar(max) NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_notification_templates] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
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
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_notification_preferences_user_id] ON [notification_preferences] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_notification_records_event_id] ON [notification_records] ([event_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_notification_records_idempotency_key] ON [notification_records] ([idempotency_key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_notification_records_status] ON [notification_records] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_notification_records_template_code] ON [notification_records] ([template_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_notification_records_user_id] ON [notification_records] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_notification_templates_code_channel] ON [notification_templates] ([code], [channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175521_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717175521_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    DROP INDEX [ix_notification_templates_code_channel] ON [notification_templates];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    CREATE UNIQUE INDEX [ix_notification_templates_code_channel] ON [notification_templates] ([code], [channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    DROP INDEX [ix_notification_records_idempotency_key] ON [notification_records];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_notification_records_idempotency_key] ON [notification_records] ([idempotency_key]) WHERE [idempotency_key] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    CREATE INDEX [ix_notification_records_channel_message_id] ON [notification_records] ([channel_message_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    CREATE INDEX [ix_notification_records_status_next_retry_at] ON [notification_records] ([status], [next_retry_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    CREATE INDEX [ix_notification_records_status_retry_count] ON [notification_records] ([status], [retry_count]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    CREATE TABLE [notification_configs] (
        [id] uniqueidentifier NOT NULL,
        [channel] int NOT NULL,
        [config_key] nvarchar(64) NOT NULL,
        [config_value] nvarchar(512) NOT NULL,
        [description] nvarchar(256) NULL,
        [is_sensitive] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        [version] rowversion NULL,
        CONSTRAINT [PK_notification_configs] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    CREATE UNIQUE INDEX [ix_notification_configs_channel_key] ON [notification_configs] ([channel], [config_key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000005_AddNotificationIndexesAndConfigTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722000005_AddNotificationIndexesAndConfigTable', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723131843_AddIxNotificationRecordsUserIsreadChannel'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [schema_version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723131843_AddIxNotificationRecordsUserIsreadChannel'
)
BEGIN
    CREATE TABLE [notification_rate_limit_configs] (
        [id] uniqueidentifier NOT NULL,
        [channel] int NOT NULL,
        [hourly_limit] int NOT NULL,
        [daily_limit] int NULL,
        [enabled] bit NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_notification_rate_limit_configs] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723131843_AddIxNotificationRecordsUserIsreadChannel'
)
BEGIN
    CREATE UNIQUE INDEX [ix_notification_rate_limit_configs_channel] ON [notification_rate_limit_configs] ([channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723131843_AddIxNotificationRecordsUserIsreadChannel'
)
BEGIN
    CREATE INDEX ix_notification_records_user_isread_channel
                      ON notification_records (user_id, is_read, channel)
                      INCLUDE (created_at, template_code)
                      WITH (ONLINE = ON, FILLFACTOR = 90);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723131843_AddIxNotificationRecordsUserIsreadChannel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723131843_AddIxNotificationRecordsUserIsreadChannel', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724000000_AddNotificationTemplateCulture'
)
BEGIN
    ALTER TABLE [notification_templates] ADD [culture] nvarchar(16) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724000000_AddNotificationTemplateCulture'
)
BEGIN
    DROP INDEX [ix_notification_templates_code_channel] ON [notification_templates];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724000000_AddNotificationTemplateCulture'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_notification_templates_code_channel] ON [notification_templates] ([code], [channel]) WHERE [culture] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724000000_AddNotificationTemplateCulture'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [uq_notification_templates_code_channel_culture] ON [notification_templates] ([code], [channel], [culture]) WHERE [culture] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724000000_AddNotificationTemplateCulture'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724000000_AddNotificationTemplateCulture', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040414_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [aggregate_root_id] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040414_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [shard_key] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040414_SyncModel20260909'
)
BEGIN
    ALTER TABLE [notification_templates] ADD [tenant_id] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040414_SyncModel20260909'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040414_SyncModel20260909'
)
BEGIN
    CREATE INDEX [ix_notification_templates_tenant_id] ON [notification_templates] ([tenant_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040414_SyncModel20260909'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909040414_SyncModel20260909', N'10.0.0');
END;

COMMIT;
GO

