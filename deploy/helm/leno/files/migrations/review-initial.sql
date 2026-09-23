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
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
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
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
)
BEGIN
    CREATE TABLE [reviews] (
        [id] uniqueidentifier NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [order_line_id] uniqueidentifier NOT NULL,
        [spu_id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [seller_id] uniqueidentifier NOT NULL,
        [rating] int NOT NULL,
        [content] nvarchar(500) NOT NULL,
        [status] int NOT NULL,
        [seller_reply_content] nvarchar(500) NULL,
        [seller_reply_by] uniqueidentifier NULL,
        [seller_reply_at] datetime2 NULL,
        [submitted_at] datetime2 NOT NULL,
        [audited_at] datetime2 NULL,
        [auditor_id] uniqueidentifier NULL,
        [hidden_at] datetime2 NULL,
        [hidden_by] uniqueidentifier NULL,
        [hide_reason] nvarchar(200) NULL,
        [append_content] nvarchar(500) NULL,
        [appended_at] datetime2 NULL,
        [version] rowversion NULL,
        [append_images] nvarchar(max) NOT NULL,
        [images] nvarchar(max) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_reviews] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_reviews_order_line_id] ON [reviews] ([order_line_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_reviews_seller_id] ON [reviews] ([seller_id]) INCLUDE ([created_at], [rating]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_reviews_spu_id] ON [reviews] ([spu_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_reviews_user_id] ON [reviews] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101305_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921101305_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

