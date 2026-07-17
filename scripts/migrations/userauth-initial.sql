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
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
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
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE TABLE [audit_logs] (
        [id] uniqueidentifier NOT NULL,
        [operator_id] uniqueidentifier NOT NULL,
        [action] nvarchar(64) NOT NULL,
        [resource_type] nvarchar(64) NOT NULL,
        [resource_id] nvarchar(64) NULL,
        [before_snapshot] nvarchar(max) NULL,
        [after_snapshot] nvarchar(max) NULL,
        [operated_at] datetime2 NOT NULL,
        [ip] nvarchar(64) NULL,
        [user_agent] nvarchar(512) NULL,
        [trace_id] nvarchar(64) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_audit_logs] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE TABLE [oauth_clients] (
        [id] uniqueidentifier NOT NULL,
        [provider] nvarchar(32) NOT NULL,
        [client_id] nvarchar(256) NOT NULL,
        [client_secret] nvarchar(512) NOT NULL,
        [redirect_uri] nvarchar(512) NOT NULL,
        [enabled] bit NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_oauth_clients] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
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
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE TABLE [roles] (
        [id] uniqueidentifier NOT NULL,
        [name] nvarchar(64) NOT NULL,
        [description] nvarchar(256) NULL,
        [permissions] nvarchar(max) NOT NULL,
        [is_built_in] bit NOT NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_roles] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE TABLE [users] (
        [id] uniqueidentifier NOT NULL,
        [username] nvarchar(32) NOT NULL,
        [email] nvarchar(256) NULL,
        [phone_number] nvarchar(20) NULL,
        [password_hash] nvarchar(128) NULL,
        [nickname] nvarchar(32) NOT NULL,
        [avatar_url] nvarchar(512) NULL,
        [status] int NOT NULL,
        [default_address_id] uniqueidentifier NULL,
        [failed_login_count] int NOT NULL,
        [locked_until] datetime2 NULL,
        [two_factor_enabled] bit NOT NULL,
        [two_factor_secret] nvarchar(256) NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_users] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE TABLE [user_external_logins] (
        [provider] nvarchar(32) NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [provider_user_id] nvarchar(256) NOT NULL,
        [email] nvarchar(256) NULL,
        [name] nvarchar(128) NULL,
        [avatar_url] nvarchar(512) NULL,
        [linked_at] datetime2 NOT NULL,
        CONSTRAINT [PK_user_external_logins] PRIMARY KEY ([user_id], [provider]),
        CONSTRAINT [FK_user_external_logins_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE TABLE [user_roles] (
        [role_type] int NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_user_roles] PRIMARY KEY ([user_id], [role_type]),
        CONSTRAINT [FK_user_roles_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_addresses_user_default] ON [addresses] ([user_id], [is_default]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_addresses_user_id] ON [addresses] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_logs_operated_at] ON [audit_logs] ([operated_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_audit_logs_operator_id] ON [audit_logs] ([operator_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_oauth_clients_provider] ON [oauth_clients] ([provider]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_roles_name] ON [roles] ([name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_user_external_logins_provider_user_id] ON [user_external_logins] ([provider], [provider_user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_users_email] ON [users] ([email]) WHERE "email" IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_users_phone_number] ON [users] ([phone_number]) WHERE "phone_number" IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [ix_users_username] ON [users] ([username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717174814_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717174814_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

