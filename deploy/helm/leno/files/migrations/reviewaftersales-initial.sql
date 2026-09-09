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
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
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
        [images] nvarchar(max) NOT NULL,
        [requested_amount] decimal(18,2) NOT NULL,
        [currency] nvarchar(8) NOT NULL,
        [approved_amount] decimal(18,2) NULL,
        [refunded_amount] decimal(18,2) NULL,
        [status] int NOT NULL,
        [applied_at] datetime2 NOT NULL,
        [approved_at] datetime2 NULL,
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
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_after_sales] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
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
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE TABLE [reviews] (
        [id] uniqueidentifier NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [order_line_id] uniqueidentifier NOT NULL,
        [spu_id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [rating] int NOT NULL,
        [content] nvarchar(500) NOT NULL,
        [images] nvarchar(max) NOT NULL,
        [status] int NOT NULL,
        [seller_reply_content] nvarchar(500) NULL,
        [submitted_at] datetime2 NOT NULL,
        [audited_at] datetime2 NULL,
        [auditor_id] uniqueidentifier NULL,
        [hidden_at] datetime2 NULL,
        [hidden_by] uniqueidentifier NULL,
        [hide_reason] nvarchar(200) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_reviews] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_after_sales_order_id] ON [after_sales] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_after_sales_seller_id] ON [after_sales] ([seller_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_after_sales_status] ON [after_sales] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_after_sales_user_id] ON [after_sales] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_reviews_order_line_id] ON [reviews] ([order_line_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_reviews_spu_id] ON [reviews] ([spu_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_reviews_user_id] ON [reviews] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175329_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717175329_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000003_AddReviewSellerFieldsAndAfterSalesAuditFields'
)
BEGIN
    ALTER TABLE [reviews] ADD [seller_id] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000003_AddReviewSellerFieldsAndAfterSalesAuditFields'
)
BEGIN
    ALTER TABLE [reviews] ADD [seller_reply_by] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000003_AddReviewSellerFieldsAndAfterSalesAuditFields'
)
BEGIN
    ALTER TABLE [reviews] ADD [seller_reply_at] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000003_AddReviewSellerFieldsAndAfterSalesAuditFields'
)
BEGIN
    ALTER TABLE [after_sales] ADD [rejected_at] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000003_AddReviewSellerFieldsAndAfterSalesAuditFields'
)
BEGIN
    ALTER TABLE [after_sales] ADD [return_confirmed_by] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000003_AddReviewSellerFieldsAndAfterSalesAuditFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722000003_AddReviewSellerFieldsAndAfterSalesAuditFields', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723131529_AddIxReviewsSellerId'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [schema_version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723131529_AddIxReviewsSellerId'
)
BEGIN
    CREATE INDEX ix_reviews_seller_id
                      ON reviews (seller_id)
                      INCLUDE (created_at, rating)
                      WITH (ONLINE = ON, FILLFACTOR = 90);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723131529_AddIxReviewsSellerId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723131529_AddIxReviewsSellerId', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040331_SyncModel20260909'
)
BEGIN
    ALTER TABLE [reviews] ADD [append_content] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040331_SyncModel20260909'
)
BEGIN
    ALTER TABLE [reviews] ADD [append_images] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040331_SyncModel20260909'
)
BEGIN
    ALTER TABLE [reviews] ADD [appended_at] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040331_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [aggregate_root_id] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040331_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [shard_key] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040331_SyncModel20260909'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040331_SyncModel20260909'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909040331_SyncModel20260909', N'10.0.0');
END;

COMMIT;
GO

