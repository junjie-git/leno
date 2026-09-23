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
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
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
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE TABLE [points_accounts] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [balance] int NOT NULL,
        [frozen_balance] int NOT NULL,
        [total_earned] int NOT NULL,
        [total_spent] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_points_accounts] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE TABLE [points_exchanges] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [target_id] uniqueidentifier NOT NULL,
        [type] int NOT NULL,
        [points_required] int NOT NULL,
        [points_account_id] uniqueidentifier NOT NULL,
        [status] int NOT NULL,
        [requested_at] datetime2 NOT NULL,
        [completed_at] datetime2 NULL,
        [reason] nvarchar(512) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_points_exchanges] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE TABLE [points_flows] (
        [id] uniqueidentifier NOT NULL,
        [account_id] uniqueidentifier NOT NULL,
        [tx_type] int NOT NULL,
        [amount] int NOT NULL,
        [balance_after] int NOT NULL,
        [source] int NOT NULL,
        [reference_id] uniqueidentifier NOT NULL,
        [reason] nvarchar(512) NOT NULL,
        [occurred_at] datetime2 NOT NULL,
        [PointsAccountId] uniqueidentifier NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_points_flows] PRIMARY KEY ([id]),
        CONSTRAINT [FK_points_flows_points_accounts_PointsAccountId] FOREIGN KEY ([PointsAccountId]) REFERENCES [points_accounts] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE TABLE [points_frozen_entries] (
        [id] uniqueidentifier NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [amount] int NOT NULL,
        [PointsAccountId] uniqueidentifier NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_points_frozen_entries] PRIMARY KEY ([id]),
        CONSTRAINT [FK_points_frozen_entries_points_accounts_PointsAccountId] FOREIGN KEY ([PointsAccountId]) REFERENCES [points_accounts] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_points_accounts_user_id] ON [points_accounts] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_points_exchanges_target_user] ON [points_exchanges] ([target_id], [user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_points_exchanges_user_id] ON [points_exchanges] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_points_flows_account_occurred] ON [points_flows] ([account_id], [occurred_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_points_flows_PointsAccountId] ON [points_flows] ([PointsAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_points_frozen_entries_order_id] ON [points_frozen_entries] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_points_frozen_entries_PointsAccountId] ON [points_frozen_entries] ([PointsAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101252_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921101252_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

