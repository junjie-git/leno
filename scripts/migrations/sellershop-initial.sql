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
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
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
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE TABLE [seller_profiles] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [real_name] nvarchar(32) NOT NULL,
        [id_card] nvarchar(18) NULL,
        [business_license_no] nvarchar(32) NULL,
        [bank_account] nvarchar(64) NULL,
        [status] int NOT NULL,
        [reviewed_by] uniqueidentifier NULL,
        [status_reason] nvarchar(200) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_seller_profiles] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE TABLE [shop_dashboard_data] (
        [id] uniqueidentifier NOT NULL,
        [shop_id] uniqueidentifier NOT NULL,
        [total_orders] int NOT NULL,
        [pending_orders] int NOT NULL,
        [completed_orders] int NOT NULL,
        [total_revenue] decimal(18,2) NOT NULL,
        [currency] nvarchar(3) NOT NULL,
        [last_updated_at] datetime2 NOT NULL,
        [version] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_shop_dashboard_data] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE TABLE [shop_metrics] (
        [id] uniqueidentifier NOT NULL,
        [shop_id] uniqueidentifier NOT NULL,
        [date] date NOT NULL,
        [order_count] int NOT NULL,
        [sales_amount] decimal(18,2) NOT NULL,
        [sales_currency] nvarchar(3) NOT NULL,
        [product_count] int NOT NULL,
        [avg_rating] decimal(5,2) NOT NULL,
        [rating_sum] decimal(10,2) NOT NULL,
        [rating_count] int NOT NULL,
        [refund_count] int NOT NULL,
        [refund_amount] decimal(18,2) NOT NULL,
        [refund_currency] nvarchar(3) NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_shop_metrics] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE TABLE [shops] (
        [id] uniqueidentifier NOT NULL,
        [seller_id] uniqueidentifier NOT NULL,
        [shop_name] nvarchar(32) NOT NULL,
        [logo] nvarchar(512) NULL,
        [description] nvarchar(1000) NULL,
        [contact_phone] nvarchar(20) NOT NULL,
        [contact_email] nvarchar(256) NULL,
        [business_license_no] nvarchar(32) NULL,
        [address] nvarchar(256) NULL,
        [status] int NOT NULL,
        [product_count] int NOT NULL,
        [status_reason] nvarchar(200) NULL,
        [reviewed_by] uniqueidentifier NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_shops] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE TABLE [shop_qualifications] (
        [id] uniqueidentifier NOT NULL,
        [shop_id] uniqueidentifier NOT NULL,
        [type] int NOT NULL,
        [number] nvarchar(64) NOT NULL,
        [image_url] nvarchar(512) NOT NULL,
        [valid_from] datetime2 NOT NULL,
        [valid_to] datetime2 NOT NULL,
        [status] int NOT NULL,
        [reject_reason] nvarchar(200) NULL,
        [reviewed_by] uniqueidentifier NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_shop_qualifications] PRIMARY KEY ([id]),
        CONSTRAINT [FK_shop_qualifications_shops_shop_id] FOREIGN KEY ([shop_id]) REFERENCES [shops] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_seller_profiles_status] ON [seller_profiles] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_seller_profiles_user_id] ON [seller_profiles] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_shop_dashboard_data_shop_id] ON [shop_dashboard_data] ([shop_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_shop_metrics_shop_date] ON [shop_metrics] ([shop_id], [date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_shop_metrics_shop_id] ON [shop_metrics] ([shop_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_shop_qualifications_shop_id] ON [shop_qualifications] ([shop_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_shop_qualifications_shop_type] ON [shop_qualifications] ([shop_id], [type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_shop_qualifications_status] ON [shop_qualifications] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_shops_seller_id] ON [shops] ([seller_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_shops_status] ON [shops] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175445_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717175445_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731020151_AddExportTasks'
)
BEGIN
    CREATE TABLE [export_tasks] (
        [id] uniqueidentifier NOT NULL,
        [shop_id] uniqueidentifier NOT NULL,
        [seller_id] uniqueidentifier NOT NULL,
        [report_type] nvarchar(64) NOT NULL,
        [start_date] datetime2 NOT NULL,
        [end_date] datetime2 NOT NULL,
        [format] nvarchar(16) NOT NULL,
        [status] nvarchar(16) NOT NULL,
        [record_count] int NULL,
        [file_size] bigint NULL,
        [file_path] nvarchar(512) NULL,
        [error_message] nvarchar(1024) NULL,
        [completed_at] datetime2 NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_export_tasks] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731020151_AddExportTasks'
)
BEGIN
    CREATE INDEX [ix_export_tasks_shop_id_status] ON [export_tasks] ([shop_id], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731020151_AddExportTasks'
)
BEGIN
    CREATE INDEX [ix_export_tasks_status_created_at] ON [export_tasks] ([status], [created_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731020151_AddExportTasks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260731020151_AddExportTasks', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    EXEC sp_rename N'[shop_dashboard_data].[UpdatedBy]', N'updated_by', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    EXEC sp_rename N'[shop_dashboard_data].[UpdatedAt]', N'updated_at', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    EXEC sp_rename N'[shop_dashboard_data].[CreatedBy]', N'created_by', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    EXEC sp_rename N'[shop_dashboard_data].[CreatedAt]', N'created_at', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[shop_dashboard_data]') AND [c].[name] = N'updated_by');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [shop_dashboard_data] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [shop_dashboard_data] ALTER COLUMN [updated_by] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[shop_dashboard_data]') AND [c].[name] = N'created_by');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [shop_dashboard_data] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [shop_dashboard_data] ALTER COLUMN [created_by] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    ALTER TABLE [shop_dashboard_data] ADD [cancelled_orders] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    ALTER TABLE [shop_dashboard_data] ADD [confirmed_orders] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    ALTER TABLE [shop_dashboard_data] ADD [refunded_amount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [aggregate_root_id] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [schema_version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [shard_key] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040350_SyncModel20260909'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909040350_SyncModel20260909', N'10.0.0');
END;

COMMIT;
GO

