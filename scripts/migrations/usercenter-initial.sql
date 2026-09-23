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
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE TABLE [addresses] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [recipient_name] nvarchar(32) NOT NULL,
        [recipient_phone] nvarchar(20) NOT NULL,
        [province] nvarchar(64) NOT NULL,
        [city] nvarchar(64) NOT NULL,
        [district] nvarchar(64) NOT NULL,
        [detail] nvarchar(200) NOT NULL,
        [tag] nvarchar(8) NULL,
        [is_default] bit NOT NULL,
        [status] int NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_addresses] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE TABLE [browse_histories] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [spu_id] uniqueidentifier NOT NULL,
        [sku_id] uniqueidentifier NULL,
        [viewed_at] datetime2 NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_browse_histories] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE TABLE [favorites] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [spu_id] uniqueidentifier NOT NULL,
        [favorited_at] datetime2 NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_favorites] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE TABLE [notification_preferences] (
        [id] uniqueidentifier NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [dnd_enabled] bit NOT NULL,
        [dnd_start] time NULL,
        [dnd_end] time NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_notification_preferences] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
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
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE TABLE [notification_preference_items] (
        [event_type] int NOT NULL,
        [notification_preferences_id] uniqueidentifier NOT NULL,
        [in_app_enabled] bit NOT NULL,
        [sms_enabled] bit NOT NULL,
        [email_enabled] bit NOT NULL,
        CONSTRAINT [PK_notification_preference_items] PRIMARY KEY ([notification_preferences_id], [event_type]),
        CONSTRAINT [FK_notification_preference_items_notification_preferences_notification_preferences_id] FOREIGN KEY ([notification_preferences_id]) REFERENCES [notification_preferences] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_addresses_user_default] ON [addresses] ([user_id], [is_default]) WHERE [is_default] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_addresses_user_id] ON [addresses] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_browse_histories_user_id] ON [browse_histories] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_browse_histories_user_spu_viewed_at] ON [browse_histories] ([user_id], [spu_id], [viewed_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_favorites_user_id] ON [favorites] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_favorites_user_spu] ON [favorites] ([user_id], [spu_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_notification_preferences_user_id] ON [notification_preferences] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_shard_status] ON [outbox_messages] ([shard_key], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921060658_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921060658_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

