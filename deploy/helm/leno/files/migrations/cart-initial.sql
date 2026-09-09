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
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
)
BEGIN
    CREATE TABLE [carts] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_carts] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
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
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
)
BEGIN
    CREATE TABLE [cart_items] (
        [id] uniqueidentifier NOT NULL,
        [cart_id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NOT NULL,
        [seller_id] uniqueidentifier NOT NULL,
        [quantity] int NOT NULL,
        [is_selected] bit NOT NULL,
        [source_cart_item_id] uniqueidentifier NOT NULL,
        [IsValid] bit NOT NULL,
        [InvalidReason] nvarchar(max) NULL,
        [DisplayTitle] nvarchar(max) NOT NULL,
        [DisplayImageUrl] nvarchar(max) NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_cart_items] PRIMARY KEY ([id]),
        CONSTRAINT [FK_cart_items_carts_cart_id] FOREIGN KEY ([cart_id]) REFERENCES [carts] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_cart_items_cart_id] ON [cart_items] ([cart_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_cart_items_seller_id] ON [cart_items] ([seller_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_cart_items_sku_id] ON [cart_items] ([sku_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_carts_user_id] ON [carts] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174927_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717174927_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_sku_id] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_sku_name] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_price] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_currency] nvarchar(8) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_main_image_url] nvarchar(1024) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_spec_text] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_available] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_version] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    ALTER TABLE [cart_items] ADD [sku_snapshot_at] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723000001_AddCartItemSkuSnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723000001_AddCartItemSkuSnapshot', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040151_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [aggregate_root_id] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040151_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [schema_version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040151_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [shard_key] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040151_SyncModel20260909'
)
BEGIN
    CREATE TABLE [cart_merge_records] (
        [anonymous_id] nvarchar(128) NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [merged_at] datetime2 NOT NULL,
        [merged_count] int NOT NULL,
        CONSTRAINT [PK_cart_merge_records] PRIMARY KEY ([anonymous_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040151_SyncModel20260909'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040151_SyncModel20260909'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909040151_SyncModel20260909', N'10.0.0');
END;

COMMIT;
GO

