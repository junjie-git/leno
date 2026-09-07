-- ============================================================================
-- Identity BC + AccessControl BC 初始迁移脚本（3.6 AuthN/AuthZ 拆分）
-- ----------------------------------------------------------------------------
-- 本脚本由 Leno 电商项目阶段三 Wave 2 子代理生成，对应：
--   - Identity BC: LenoIdentity 数据库（users / user_external_logins / refresh_tokens
--                  / two_factor_sessions / oauth_clients / outbox_messages）
--   - AccessControl BC: LenoAccessControl 数据库的 user_role_assignments 表
--     （由 AccessControl.Infrastructure 的 AccessControlDbContext 创建，
--      本脚本不在 LenoAccessControl 库内创建重复表，仅在此注释说明角色数据流向）
--
-- 角色数据迁移说明：
--   原 UserAuth BC 的 users.user_roles 内联集合（user_roles 表）已废止，
--   角色信息迁入 AccessControl BC 的 user_role_assignments 表（由 UserRoleAssignment 聚合承载）。
--   Identity BC 不再持久化角色，JWT 阶段通过 AccessControl BC 的 GetUserRoles RPC 实时获取角色 claims。
--
-- 数据库：LenoIdentity（由 ConnectionStrings:IdentityDb 指向）
-- 适用环境：开发/测试/生产（生产环境须先备份后执行）
-- ============================================================================

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

DECLARE @MigrationId nvarchar(150) = N'20260723000000_IdentityAccessControlInitial';

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    -- ----------------------------------------------------------------------
    -- users 表（迁自 UserAuth.users，移除 roles 字段；角色由 AccessControl BC 承载）
    -- ----------------------------------------------------------------------
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
        [row_version] rowversion NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        [created_by] nvarchar(64) NULL,
        [updated_by] nvarchar(64) NULL,
        CONSTRAINT [PK_users] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    -- ----------------------------------------------------------------------
    -- user_external_logins 表（User 聚合的 owned collection）
    -- ----------------------------------------------------------------------
    CREATE TABLE [user_external_logins] (
        [user_id] uniqueidentifier NOT NULL,
        [provider] nvarchar(32) NOT NULL,
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
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    -- ----------------------------------------------------------------------
    -- refresh_tokens 表（RefreshToken 聚合根，承载轮换与撤销状态）
    -- ----------------------------------------------------------------------
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
        CONSTRAINT [PK_refresh_tokens] PRIMARY KEY ([id]),
        CONSTRAINT [FK_refresh_tokens_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    -- ----------------------------------------------------------------------
    -- two_factor_sessions 表（TwoFactorSession 聚合根）
    -- 注意：created_at 列对应 TwoFactorSession.CreatedAt（new 隐藏基类属性），
    -- audit_created_at/audit_updated_at/audit_created_by/audit_updated_by 为基类审计字段 shadow property。
    -- ----------------------------------------------------------------------
    CREATE TABLE [two_factor_sessions] (
        [id] uniqueidentifier NOT NULL,
        [temp_token] nvarchar(128) NOT NULL,
        [user_id] uniqueidentifier NOT NULL,
        [status] int NOT NULL,
        [created_at] datetime2 NOT NULL,
        [expires_at] datetime2 NOT NULL,
        [verified_at] datetime2 NULL,
        [attempt_count] int NOT NULL,
        [audit_created_at] datetime2 NOT NULL,
        [audit_updated_at] datetime2 NOT NULL,
        [audit_created_by] nvarchar(64) NULL,
        [audit_updated_by] nvarchar(64) NULL,
        [version] rowversion NULL,
        CONSTRAINT [PK_two_factor_sessions] PRIMARY KEY ([id]),
        CONSTRAINT [FK_two_factor_sessions_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    -- ----------------------------------------------------------------------
    -- oauth_clients 表（OAuth2 客户端配置聚合根）
    -- ----------------------------------------------------------------------
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
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    -- ----------------------------------------------------------------------
    -- outbox_messages 表（BaseDbContext 共享的发件箱，承载领域事件 → 集成事件）
    -- ----------------------------------------------------------------------
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

-- ============================================================================
-- 索引
-- ============================================================================

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    CREATE UNIQUE INDEX [ix_users_username] ON [users] ([username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_users_email] ON [users] ([email]) WHERE [email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [ix_users_phone_number] ON [users] ([phone_number]) WHERE [phone_number] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    CREATE UNIQUE INDEX [ix_user_external_logins_provider_user_id] ON [user_external_logins] ([provider], [provider_user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    CREATE UNIQUE INDEX [ix_refresh_tokens_token] ON [refresh_tokens] ([token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    CREATE INDEX [ix_refresh_tokens_user_id] ON [refresh_tokens] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    CREATE UNIQUE INDEX [ix_two_factor_sessions_temp_token] ON [two_factor_sessions] ([temp_token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    CREATE INDEX [ix_two_factor_sessions_user_id] ON [two_factor_sessions] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    CREATE UNIQUE INDEX [ix_oauth_clients_provider] ON [oauth_clients] ([provider]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    CREATE INDEX [ix_outbox_messages_status] ON [outbox_messages] ([status]);
END;

-- ============================================================================
-- 记录迁移历史
-- ============================================================================

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = @MigrationId
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (@MigrationId, N'10.0.9');
END;

COMMIT TRANSACTION;
GO

-- ============================================================================
-- 角色数据迁移说明（不在本数据库执行，仅记录迁移路径）
-- ----------------------------------------------------------------------------
-- 原 UserAuth BC 的 user_roles 表数据须迁入 AccessControl BC 的 LenoAccessControl 数据库
-- 的 user_role_assignments 表（UserRoleAssignment 聚合），字段映射：
--   user_roles.user_id      -> user_role_assignments.user_id
--   user_roles.role_type    -> user_role_assignments.role（RoleType 枚举值保持一致）
--   (新增) assigned_at       -> 默认 CURRENT_TIMESTAMP 或迁移时间戳
--   (新增) assigned_by       -> 系统/迁移操作员标识
--   (新增) is_active         -> true
-- 详细迁移 SQL 由 AccessControl BC 迁移脚本负责，此处仅作记录。
-- ============================================================================
