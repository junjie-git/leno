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
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
)
BEGIN
    CREATE TABLE [member_level_definitions] (
        [id] uniqueidentifier NOT NULL,
        [level] int NOT NULL,
        [name] nvarchar(64) NOT NULL,
        [min_growth_value] int NOT NULL,
        [max_growth_value] int NOT NULL,
        [description] nvarchar(512) NOT NULL,
        [level_up_bonus_points] int NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_member_level_definitions] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
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
        [growth_value] int NOT NULL,
        [growth_value_updated_at] datetime2 NOT NULL,
        [current_growth_level] int NOT NULL,
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
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
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
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
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
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
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
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_member_level_definitions_level] ON [member_level_definitions] ([level]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_members_user_id] ON [members] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_membership_packages_level] ON [membership_packages] ([level]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921101258_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921101258_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

