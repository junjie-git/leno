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
    VALUES (N'20260717174606_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

