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
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
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
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE TABLE [payment_channel_configs] (
        [id] uniqueidentifier NOT NULL,
        [channel] int NOT NULL,
        [config_name] nvarchar(128) NOT NULL,
        [config_value] nvarchar(max) NOT NULL,
        [description] nvarchar(500) NULL,
        [enabled] bit NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_payment_channel_configs] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE TABLE [payment_orders] (
        [id] uniqueidentifier NOT NULL,
        [out_trade_no] nvarchar(64) NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [amount] decimal(18,2) NOT NULL,
        [currency] nvarchar(8) NOT NULL,
        [channel] int NOT NULL,
        [channel_trade_no] nvarchar(128) NULL,
        [status] int NOT NULL,
        [prepay_id] nvarchar(128) NULL,
        [code_url] nvarchar(512) NULL,
        [h5_url] nvarchar(512) NULL,
        [expire_at] datetime2 NOT NULL,
        [paid_at] datetime2 NULL,
        [fail_reason] nvarchar(512) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_payment_orders] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE TABLE [ReconciliationDiffs] (
        [Id] uniqueidentifier NOT NULL,
        [BillDate] datetime2 NOT NULL,
        [Channel] nvarchar(450) NOT NULL,
        [DiffType] nvarchar(max) NOT NULL,
        [ChannelTransactionNo] nvarchar(128) NULL,
        [ChannelAmount] decimal(18,2) NULL,
        [ChannelTransactionTime] datetime2 NULL,
        [SystemTransactionNo] nvarchar(128) NULL,
        [SystemAmount] decimal(18,2) NULL,
        [PaymentId] uniqueidentifier NULL,
        [Remark] nvarchar(500) NULL,
        [Status] nvarchar(max) NOT NULL,
        [version] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ReconciliationDiffs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE TABLE [refund_orders] (
        [id] uniqueidentifier NOT NULL,
        [out_refund_no] nvarchar(64) NOT NULL,
        [out_trade_no] nvarchar(64) NOT NULL,
        [payment_id] uniqueidentifier NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [after_sales_id] uniqueidentifier NOT NULL,
        [refund_amount] decimal(18,2) NOT NULL,
        [currency] nvarchar(8) NOT NULL,
        [channel] int NOT NULL,
        [channel_refund_no] nvarchar(128) NULL,
        [status] int NOT NULL,
        [refunded_at] datetime2 NULL,
        [fail_reason] nvarchar(512) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_refund_orders] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_payment_channel_configs_channel] ON [payment_channel_configs] ([channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_payment_channel_configs_channel_name] ON [payment_channel_configs] ([channel], [config_name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_payment_orders_order_id] ON [payment_orders] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_payment_orders_out_trade_no] ON [payment_orders] ([out_trade_no]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_payment_orders_status] ON [payment_orders] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_payment_orders_user_id] ON [payment_orders] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReconciliationDiffs_BillDate] ON [ReconciliationDiffs] ([BillDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReconciliationDiffs_BillDate_Channel] ON [ReconciliationDiffs] ([BillDate], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_refund_orders_after_sales_id] ON [refund_orders] ([after_sales_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_refund_orders_order_id] ON [refund_orders] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_refund_orders_out_refund_no] ON [refund_orders] ([out_refund_no]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_refund_orders_payment_id] ON [refund_orders] ([payment_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175039_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717175039_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

