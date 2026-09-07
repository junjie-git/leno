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
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [check_in_records] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [check_in_date] date NOT NULL,
        [continuous_days] int NOT NULL,
        [points_awarded] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_check_in_records] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [MemberLevels] (
        [Id] uniqueidentifier NOT NULL,
        [Level] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [MinGrowthValue] int NOT NULL,
        [MaxGrowthValue] int NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [version] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MemberLevels] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [members] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [current_level] int NOT NULL,
        [total_consumption] decimal(18,2) NOT NULL,
        [joined_at] datetime2 NOT NULL,
        [level_upgraded_at] datetime2 NOT NULL,
        [status] int NOT NULL,
        [GrowthValue] int NOT NULL,
        [GrowthValueUpdatedAt] datetime2 NOT NULL,
        [CurrentGrowthLevel] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_members] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [membership_levels] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(64) NOT NULL,
        [level] int NOT NULL,
        [min_consumption] decimal(18,2) NOT NULL,
        [discount_rate] decimal(3,2) NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_membership_levels] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [membership_packages] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [level] int NOT NULL,
        [price] decimal(18,2) NOT NULL,
        [duration_days] int NOT NULL,
        [benefits] nvarchar(max) NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_membership_packages] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
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
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
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
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [tasks] (
        [id] uniqueidentifier NOT NULL,
        [type] int NOT NULL,
        [name] nvarchar(128) NOT NULL,
        [description] nvarchar(512) NOT NULL,
        [reward_points] int NOT NULL,
        [completion_condition] nvarchar(256) NOT NULL,
        [is_daily] bit NOT NULL,
        [is_one_time] bit NOT NULL,
        [is_enabled] bit NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_tasks] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [user_memberships] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [package_id] uniqueidentifier NOT NULL,
        [level] int NOT NULL,
        [start_time] datetime2 NOT NULL,
        [end_time] datetime2 NOT NULL,
        [status] int NOT NULL,
        [order_id] uniqueidentifier NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_user_memberships] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [user_tasks] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [task_id] uniqueidentifier NOT NULL,
        [status] int NOT NULL,
        [completed_at] datetime2 NULL,
        [completed_date] date NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_user_tasks] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [member_level_change_histories] (
        [MemberId] uniqueidentifier NOT NULL,
        [Id] int NOT NULL IDENTITY,
        [old_level] int NOT NULL,
        [new_level] int NOT NULL,
        [growth_value] int NOT NULL,
        [changed_at] datetime2 NOT NULL,
        [reason] nvarchar(512) NOT NULL,
        CONSTRAINT [PK_member_level_change_histories] PRIMARY KEY ([MemberId], [Id]),
        CONSTRAINT [FK_member_level_change_histories_members_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [members] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [points_frozen_entries] (
        [id] uniqueidentifier NOT NULL,
        [order_id] uniqueidentifier NOT NULL,
        [amount] int NOT NULL,
        [points_account_id] uniqueidentifier NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_points_frozen_entries] PRIMARY KEY ([id]),
        CONSTRAINT [FK_points_frozen_entries_points_accounts_points_account_id] FOREIGN KEY ([points_account_id]) REFERENCES [points_accounts] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE TABLE [points_ledgers] (
        [id] uniqueidentifier NOT NULL,
        [account_id] uniqueidentifier NOT NULL,
        [tx_type] int NOT NULL,
        [amount] int NOT NULL,
        [balance_after] int NOT NULL,
        [source] int NOT NULL,
        [reference_id] uniqueidentifier NOT NULL,
        [reason] nvarchar(256) NOT NULL,
        [occurred_at] datetime2 NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_points_ledgers] PRIMARY KEY ([id]),
        CONSTRAINT [FK_points_ledgers_points_accounts_account_id] FOREIGN KEY ([account_id]) REFERENCES [points_accounts] ([id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_check_in_records_user_id_check_in_date] ON [check_in_records] ([user_id], [check_in_date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_members_user_id] ON [members] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_membership_levels_level] ON [membership_levels] ([level]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_membership_packages_status] ON [membership_packages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_points_accounts_user_id] ON [points_accounts] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_points_frozen_entries_order_id] ON [points_frozen_entries] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_points_frozen_entries_points_account_id] ON [points_frozen_entries] ([points_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_points_ledgers_account_id_occurred_at] ON [points_ledgers] ([account_id], [occurred_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_tasks_type] ON [tasks] ([type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_user_memberships_order_id] ON [user_memberships] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_user_memberships_user_id] ON [user_memberships] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_user_tasks_user_id_task_id] ON [user_tasks] ([user_id], [task_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717175251_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717175251_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

