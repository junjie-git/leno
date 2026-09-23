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
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
)
BEGIN
    CREATE TABLE [after_sales] (
        [id] uniqueidentifier NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [order_line_id] uniqueidentifier NULL,
        [user_id] uniqueidentifier NOT NULL,
        [seller_id] uniqueidentifier NOT NULL,
        [type] int NOT NULL,
        [reason_category] nvarchar(64) NOT NULL,
        [reason] nvarchar(500) NOT NULL,
        [requested_amount] decimal(18,2) NOT NULL,
        [currency] nvarchar(8) NOT NULL,
        [approved_amount] decimal(18,2) NULL,
        [refunded_amount] decimal(18,2) NULL,
        [status] int NOT NULL,
        [applied_at] datetime2 NOT NULL,
        [approved_at] datetime2 NULL,
        [rejected_at] datetime2 NULL,
        [approver_id] uniqueidentifier NULL,
        [refunded_at] datetime2 NULL,
        [channel_refund_no] nvarchar(128) NULL,
        [reject_reason] nvarchar(200) NULL,
        [fail_reason] nvarchar(512) NULL,
        [cancelled_at] datetime2 NULL,
        [cancel_reason] nvarchar(200) NULL,
        [returned_at] datetime2 NULL,
        [tracking_no] nvarchar(64) NULL,
        [return_confirmed_at] datetime2 NULL,
        [return_confirmed_by] uniqueidentifier NULL,
        [version] rowversion NULL,
        [images] nvarchar(max) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_after_sales] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
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
        [schema_version] int NOT NULL DEFAULT 1,
        [aggregate_root_id] uniqueidentifier NOT NULL,
        [shard_key] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_outbox_messages] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_after_sales_order_id] ON [after_sales] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_after_sales_seller_id] ON [after_sales] ([seller_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_after_sales_status] ON [after_sales] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_after_sales_user_id] ON [after_sales] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101312_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921101312_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

