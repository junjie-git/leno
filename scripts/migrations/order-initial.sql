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
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE TABLE [freight_templates] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [type] int NOT NULL,
        [free_shipping_threshold] decimal(18,2) NULL,
        [seller_id] uniqueidentifier NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_freight_templates] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE TABLE [logistics_companies] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [code] nvarchar(64) NOT NULL,
        [service_phone] nvarchar(32) NULL,
        [support_tracking] bit NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_logistics_companies] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE TABLE [orders] (
        [id] uniqueidentifier NOT NULL,
        [order_no] nvarchar(64) NOT NULL,
        [order_type] int NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [seller_id] uniqueidentifier NULL,
        [items_amount] decimal(18,2) NOT NULL,
        [discount_amount] decimal(18,2) NOT NULL,
        [points_offset_amount] decimal(18,2) NOT NULL,
        [freight_amount] decimal(18,2) NOT NULL,
        [total_amount] decimal(18,2) NOT NULL,
        [status] int NOT NULL,
        [recipient_name] nvarchar(64) NOT NULL,
        [recipient_phone] nvarchar(32) NOT NULL,
        [province] nvarchar(64) NOT NULL,
        [city] nvarchar(64) NOT NULL,
        [district] nvarchar(64) NOT NULL,
        [address_detail] nvarchar(256) NOT NULL,
        [payment_method] int NULL,
        [payment_initiated] bit NOT NULL,
        [payment_initiated_at] datetime2 NULL,
        [expire_at] datetime2 NOT NULL,
        [paid_at] datetime2 NULL,
        [payment_id] uniqueidentifier NULL,
        [trade_no] nvarchar(128) NULL,
        [shipped_at] datetime2 NULL,
        [logistics_no] nvarchar(128) NULL,
        [LogisticsCompanyCode] nvarchar(max) NULL,
        [completed_at] datetime2 NULL,
        [after_sales_window_ends_at] datetime2 NULL,
        [cancelled_at] datetime2 NULL,
        [cancel_reason] nvarchar(512) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_orders] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
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
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE TABLE [stock_reservation_compensations] (
        [id] uniqueidentifier NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NOT NULL,
        [quantity] int NOT NULL,
        [status] int NOT NULL,
        [retry_count] int NOT NULL,
        [max_retries] int NOT NULL,
        [last_attempted_at] datetime2 NULL,
        [last_error_message] nvarchar(500) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_stock_reservation_compensations] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE TABLE [stock_reservations] (
        [id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NOT NULL,
        [base_line_qty] int NOT NULL,
        [reserved_qty] int NOT NULL,
        [deducted_qty] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_stock_reservations] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE TABLE [freight_region_rules] (
        [region_code] nvarchar(32) NOT NULL,
        [first_unit] int NOT NULL,
        [first_price] decimal(18,2) NOT NULL,
        [additional_unit] int NOT NULL,
        [additional_price] decimal(18,2) NOT NULL,
        [FreightTemplateId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_freight_region_rules] PRIMARY KEY ([region_code]),
        CONSTRAINT [FK_freight_region_rules_freight_templates_FreightTemplateId] FOREIGN KEY ([FreightTemplateId]) REFERENCES [freight_templates] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE TABLE [order_items] (
        [id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NOT NULL,
        [product_sku_id] uniqueidentifier NOT NULL,
        [product_spu_id] uniqueidentifier NOT NULL,
        [product_name] nvarchar(256) NOT NULL,
        [product_sku_name] nvarchar(256) NOT NULL,
        [product_main_image] nvarchar(512) NULL,
        [product_seller_id] uniqueidentifier NOT NULL,
        [unit_price] decimal(18,2) NOT NULL,
        [quantity] int NOT NULL,
        [discount_allocation] decimal(18,2) NOT NULL,
        [subtotal] decimal(18,2) NOT NULL,
        [source_cart_item_id] uniqueidentifier NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_order_items] PRIMARY KEY ([id]),
        CONSTRAINT [FK_order_items_orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [orders] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_freight_region_rules_FreightTemplateId] ON [freight_region_rules] ([FreightTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_freight_templates_seller_id] ON [freight_templates] ([seller_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_logistics_companies_code] ON [logistics_companies] ([code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_OrderId] ON [order_items] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_orders_order_no] ON [orders] ([order_no]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_orders_seller_id] ON [orders] ([seller_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_orders_status] ON [orders] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_orders_user_id] ON [orders] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_stock_compensations_order_id] ON [stock_reservation_compensations] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_stock_compensations_status] ON [stock_reservation_compensations] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_stock_reservations_sku_id] ON [stock_reservations] ([sku_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174606_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717174606_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000002_AddOrderRowVersionAndSoftDelete'
)
BEGIN
    ALTER TABLE [orders] ADD [row_version] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000002_AddOrderRowVersionAndSoftDelete'
)
BEGIN
    ALTER TABLE [orders] ADD [is_deleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000002_AddOrderRowVersionAndSoftDelete'
)
BEGIN
    ALTER TABLE [orders] ADD [deleted_at] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000002_AddOrderRowVersionAndSoftDelete'
)
BEGIN
    CREATE INDEX [ix_orders_is_deleted] ON [orders] ([is_deleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722000002_AddOrderRowVersionAndSoftDelete'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722000002_AddOrderRowVersionAndSoftDelete', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723100000_DropOrderVersionShadowColumn'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[orders]') AND [c].[name] = N'version');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [orders] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [orders] DROP COLUMN [version];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723100000_DropOrderVersionShadowColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723100000_DropOrderVersionShadowColumn', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723110000_AddStockCompensationOperationType'
)
BEGIN
    ALTER TABLE [stock_reservation_compensations] ADD [operation_type] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723110000_AddStockCompensationOperationType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723110000_AddStockCompensationOperationType', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723164721_AddOrderSagaStates'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [schema_version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723164721_AddOrderSagaStates'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[orders]') AND [c].[name] = N'row_version');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [orders] DROP CONSTRAINT ' + @var1 + ';');
    EXEC(N'UPDATE [orders] SET [row_version] = 0x WHERE [row_version] IS NULL');
    ALTER TABLE [orders] ALTER COLUMN [row_version] rowversion NOT NULL;
    ALTER TABLE [orders] ADD DEFAULT 0x FOR [row_version];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723164721_AddOrderSagaStates'
)
BEGIN
    CREATE TABLE [order_saga_states] (
        [correlation_id] uniqueidentifier NOT NULL,
        [current_state] nvarchar(32) NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [total_amount] decimal(18,2) NOT NULL,
        [currency] nvarchar(8) NOT NULL,
        [items_json] nvarchar(max) NOT NULL,
        [stock_reservation_ids_json] nvarchar(max) NULL,
        [points_frozen_amount] decimal(18,2) NOT NULL,
        [payment_id] uniqueidentifier NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [row_version] rowversion NOT NULL,
        CONSTRAINT [PK_order_saga_states] PRIMARY KEY ([correlation_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723164721_AddOrderSagaStates'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_stock_compensations_order_sku_pending] ON [stock_reservation_compensations] ([order_id], [sku_id]) WHERE [status] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723164721_AddOrderSagaStates'
)
BEGIN
    CREATE INDEX [ix_order_saga_states_current_state] ON [order_saga_states] ([current_state]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723164721_AddOrderSagaStates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723164721_AddOrderSagaStates', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723182253_AddOrderPaymentProcesses'
)
BEGIN
    CREATE TABLE [order_payment_processes] (
        [process_id] uniqueidentifier NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [payment_id] uniqueidentifier NOT NULL,
        [current_state] nvarchar(32) NOT NULL,
        [stock_confirmed] bit NOT NULL,
        [points_confirmed] bit NOT NULL,
        [order_marked_paid] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [row_version] rowversion NOT NULL,
        CONSTRAINT [PK_order_payment_processes] PRIMARY KEY ([process_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723182253_AddOrderPaymentProcesses'
)
BEGIN
    CREATE INDEX [ix_order_payment_processes_current_state] ON [order_payment_processes] ([current_state]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723182253_AddOrderPaymentProcesses'
)
BEGIN
    CREATE UNIQUE INDEX [ix_order_payment_processes_order_id] ON [order_payment_processes] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723182253_AddOrderPaymentProcesses'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723182253_AddOrderPaymentProcesses', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723190000_AddReadModelSnapshots'
)
BEGIN
    CREATE TABLE [read_model_snapshots] (
        [aggregate_id] nvarchar(128) NOT NULL,
        [aggregate_type] nvarchar(128) NOT NULL,
        [version] bigint NOT NULL,
        [state_json] nvarchar(max) NOT NULL,
        [taken_at] datetime2 NOT NULL,
        CONSTRAINT [PK_read_model_snapshots] PRIMARY KEY ([aggregate_id], [version])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723190000_AddReadModelSnapshots'
)
BEGIN
    CREATE INDEX [ix_read_model_snapshots_aggregate_type] ON [read_model_snapshots] ([aggregate_type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723190000_AddReadModelSnapshots'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723190000_AddReadModelSnapshots', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040215_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [aggregate_root_id] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040215_SyncModel20260909'
)
BEGIN
    ALTER TABLE [outbox_messages] ADD [shard_key] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040215_SyncModel20260909'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909040215_SyncModel20260909'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909040215_SyncModel20260909', N'10.0.0');
END;

COMMIT;
GO

