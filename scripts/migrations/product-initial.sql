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
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE TABLE [brands] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(50) NOT NULL,
        [logo] nvarchar(512) NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_brands] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE TABLE [categories] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(50) NOT NULL,
        [parent_id] uniqueidentifier NULL,
        [level] int NOT NULL,
        [sort_order] int NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_categories] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
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
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE TABLE [spus] (
        [id] uniqueidentifier NOT NULL,
        [shop_id] uniqueidentifier NOT NULL,
        [seller_id] uniqueidentifier NOT NULL,
        [title] nvarchar(100) NOT NULL,
        [subtitle] nvarchar(200) NULL,
        [main_image_url] nvarchar(512) NOT NULL,
        [category_id] uniqueidentifier NOT NULL,
        [brand_id] uniqueidentifier NULL,
        [status] int NOT NULL,
        [specs] nvarchar(max) NOT NULL,
        [suspended_by_shop] bit NOT NULL,
        [reviewed_by] uniqueidentifier NULL,
        [average_score] float NOT NULL,
        [review_count] int NOT NULL,
        [version] rowversion NULL,
        [audit_history] nvarchar(max) NOT NULL,
        [price_change_history] nvarchar(max) NOT NULL,
        [stock_operation_history] nvarchar(max) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_spus] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE TABLE [stock_baselines] (
        [id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NOT NULL,
        [available_qty] int NOT NULL,
        [reserved_qty] int NOT NULL,
        [deducted_qty] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_stock_baselines] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE TABLE [skus] (
        [id] uniqueidentifier NOT NULL,
        [spu_id] uniqueidentifier NOT NULL,
        [sku_code] nvarchar(64) NOT NULL,
        [price] decimal(18,2) NOT NULL,
        [currency] nvarchar(3) NOT NULL,
        [stock_qty] int NOT NULL,
        [spec_attributes] nvarchar(max) NOT NULL,
        [status] int NOT NULL,
        [image_url] nvarchar(512) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_skus] PRIMARY KEY ([id]),
        CONSTRAINT [FK_skus_spus_spu_id] FOREIGN KEY ([spu_id]) REFERENCES [spus] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE TABLE [spu_images] (
        [SPUId] uniqueidentifier NOT NULL,
        [Id] int NOT NULL IDENTITY,
        [url] nvarchar(512) NOT NULL,
        [sort_order] int NOT NULL,
        [is_main] bit NOT NULL,
        CONSTRAINT [PK_spu_images] PRIMARY KEY ([SPUId], [Id]),
        CONSTRAINT [FK_spu_images_spus_SPUId] FOREIGN KEY ([SPUId]) REFERENCES [spus] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_brands_name] ON [brands] ([name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_categories_parent_id] ON [categories] ([parent_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_skus_sku_code] ON [skus] ([sku_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_skus_spu_id] ON [skus] ([spu_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_spus_category_id] ON [spus] ([category_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_spus_shop_id] ON [spus] ([shop_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_spus_status] ON [spus] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_stock_baselines_sku_id] ON [stock_baselines] ([sku_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174853_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717174853_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

