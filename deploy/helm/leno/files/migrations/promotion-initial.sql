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
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE TABLE [coupons] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [type] int NOT NULL,
        [face_value] decimal(18,2) NOT NULL,
        [min_spend] decimal(18,2) NOT NULL,
        [validity_type] int NOT NULL,
        [valid_from] datetime2 NULL,
        [valid_to] datetime2 NULL,
        [valid_days] int NULL,
        [total_qty] int NOT NULL,
        [issued_qty] int NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_coupons] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
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
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE TABLE [promotion_activities] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [type] int NOT NULL,
        [status] int NOT NULL,
        [start_time] datetime2 NOT NULL,
        [end_time] datetime2 NOT NULL,
        [rules] nvarchar(max) NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_promotion_activities] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE TABLE [seckill_activities] (
        [id] uniqueidentifier NOT NULL,
        [spu_id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NOT NULL,
        [seckill_price] decimal(18,2) NOT NULL,
        [original_price] decimal(18,2) NOT NULL,
        [total_stock] int NOT NULL,
        [available_stock] int NOT NULL,
        [limit_per_user] int NOT NULL,
        [start_time] datetime2 NOT NULL,
        [end_time] datetime2 NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_seckill_activities] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE TABLE [SeckillPreOccupationRecords] (
        [Id] uniqueidentifier NOT NULL,
        [ActivityId] uniqueidentifier NOT NULL,
        [SkuId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [PreOccupiedAt] datetime2 NOT NULL,
        [IsFulfilled] bit NOT NULL,
        [FulfilledAt] datetime2 NULL,
        [IsRolledBack] bit NOT NULL,
        [RolledBackAt] datetime2 NULL,
        [version] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SeckillPreOccupationRecords] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE TABLE [user_coupons] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [coupon_id] uniqueidentifier NOT NULL,
        [status] int NOT NULL,
        [source] nvarchar(32) NOT NULL,
        [received_at] datetime2 NOT NULL,
        [used_at] datetime2 NULL,
        [used_order_id] uniqueidentifier NULL,
        [locked_order_id] uniqueidentifier NULL,
        [expired_at] datetime2 NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_user_coupons] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_coupons_status] ON [coupons] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_promotion_activities_status] ON [promotion_activities] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_seckill_activities_sku_id] ON [seckill_activities] ([sku_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_seckill_activities_status] ON [seckill_activities] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SeckillPreOccupationRecords_IsFulfilled_IsRolledBack_PreOccupiedAt] ON [SeckillPreOccupationRecords] ([IsFulfilled], [IsRolledBack], [PreOccupiedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SeckillPreOccupationRecords_OrderId] ON [SeckillPreOccupationRecords] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_user_coupons_coupon_id] ON [user_coupons] ([coupon_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_user_coupons_locked_order_id] ON [user_coupons] ([locked_order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_user_coupons_user_id] ON [user_coupons] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ux_user_coupons_user_id_coupon_id] ON [user_coupons] ([user_id], [coupon_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175003_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717175003_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

