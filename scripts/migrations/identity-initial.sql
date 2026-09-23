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
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE TABLE [oauth_clients] (
        [id] uniqueidentifier NOT NULL,
        [provider] nvarchar(32) NOT NULL,
        [provider_type] nvarchar(16) NOT NULL,
        [discovery_url] nvarchar(512) NULL,
        [client_id] nvarchar(256) NOT NULL,
        [client_secret] nvarchar(512) NOT NULL,
        [redirect_uri] nvarchar(512) NOT NULL,
        [scopes] nvarchar(max) NOT NULL,
        [claim_mappings] nvarchar(max) NOT NULL,
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
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
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
        CONSTRAINT [PK_outbox_messages] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE TABLE [refresh_tokens] (
        [id] uniqueidentifier NOT NULL,
        [token] nvarchar(128) NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [issued_at] datetime2 NOT NULL,
        [expires_at] datetime2 NOT NULL,
        [revoked_at] datetime2 NULL,
        [revoke_reason] nvarchar(64) NULL,
        [replaced_by_id] uniqueidentifier NULL,
        [version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_refresh_tokens] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE TABLE [two_factor_sessions] (
        [id] uniqueidentifier NOT NULL,
        [temp_token] nvarchar(128) NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [status] int NOT NULL,
        [audit_created_at] datetime2 NOT NULL,
        [expires_at] datetime2 NOT NULL,
        [verified_at] datetime2 NULL,
        [attempt_count] int NOT NULL,
        [version] rowversion NULL,
        [audit_updated_at] datetime2 NOT NULL,
        [audit_created_by] nvarchar(64) NULL,
        [audit_updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_two_factor_sessions] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE TABLE [users] (
        [id] uniqueidentifier NOT NULL,
        [username] nvarchar(32) NOT NULL,
        [email] nvarchar(256) NULL,
        [phone_number] nvarchar(20) NULL,
        [password_hash] nvarchar(256) NULL,
        [nickname] nvarchar(32) NOT NULL,
        [avatar_url] nvarchar(512) NULL,
        [status] int NOT NULL,
        [default_address_id] uniqueidentifier NULL,
        [failed_login_count] int NOT NULL,
        [locked_until] datetime2 NULL,
        [two_factor_enabled] bit NOT NULL,
        [two_factor_secret] nvarchar(256) NULL,
        [row_version] rowversion NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_users] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
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
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE UNIQUE INDEX [ix_oauth_clients_provider] ON [oauth_clients] ([provider]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE UNIQUE INDEX [ix_refresh_tokens_token] ON [refresh_tokens] ([token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE INDEX [ix_refresh_tokens_user_id] ON [refresh_tokens] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE UNIQUE INDEX [ix_two_factor_sessions_temp_token] ON [two_factor_sessions] ([temp_token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE INDEX [ix_two_factor_sessions_user_id] ON [two_factor_sessions] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE UNIQUE INDEX [ix_user_external_logins_provider_user_id] ON [user_external_logins] ([provider], [provider_user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_users_email] ON [users] ([email]) WHERE [email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_users_phone_number] ON [users] ([phone_number]) WHERE [phone_number] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    CREATE UNIQUE INDEX [ix_users_username] ON [users] ([username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723181050_ExtendOAuthClientForOidc'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723181050_ExtendOAuthClientForOidc', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723193852_AddPasswordHashVersionColumn'
)
BEGIN
    ALTER TABLE [users] ADD [password_hash_version] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723193852_AddPasswordHashVersionColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723193852_AddPasswordHashVersionColumn', N'10.0.0');
END;

COMMIT;
GO

