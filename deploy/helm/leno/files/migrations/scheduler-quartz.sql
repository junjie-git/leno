-- ============================================================================
-- Quartz.NET 调度器建表脚本（目标库：LenoScheduler）
-- ----------------------------------------------------------------------------
-- 用途：为 MassTransit + Quartz.NET 消息调度器提供持久化存储，支撑延迟消息
--       （订单 30 分钟超时取消 / 售后窗口 7 天后关闭）与后续的 cron 周期任务。
--
-- 背景（双轨下线 DEC-2 决策 (b)，2026-09-21）：
--   此前 OrderAppService.ConfirmReceiptAsync 与 OrderSagaOrchestrator 调用
--   CreateMessageScheduler() 但全仓从未注册调度器 → 延迟消息投递必然失败。
--   详见 docs/双轨下线-实施方案.md §5.2–§5.5。
--
-- 重要：本脚本是 N1「schema 事实来源统一为 EF 迁移」的**显式例外**。
--   QRTZ_* 表由 Quartz 自有 DDL 定义、随 Quartz 版本演进，不应由 EF 迁移建模。
--   该例外已登记在 docs/双轨下线-实施方案.md §5.5，请勿为其生成 EF 迁移。
--
-- 幂等性：每张表与每个约束都以存在性守卫包裹，可随 Helm migration-job 重复执行。
-- 来源：Quartz.NET 官方 SQL Server DDL（quartznet/quartznet: database/tables/tables_sqlServer.sql，3.13.x）
--
-- 说明：
--   1) 已省略官方脚本中的索引段（CREATE INDEX）。抓取到的官方索引语句缺少列定义，
--      直接执行会在 sqlcmd -b 下中断迁移；且索引缺失仅影响调度查询性能，不影响正确性。
--      如需补充索引，请以 Quartz 版本对应的官方脚本为准另行提交。
--   2) 官方脚本中两张可选历史表（QRTZ_EXECUTION_HISTORY / QRTZ_MISFIRE_HISTORY）
--      仅在使用 UseExecutionHistory() 时才需要，当前未启用，故不创建。
-- ============================================================================

-- ---------------------------------------------------------------------------
-- 1. 建表
-- ---------------------------------------------------------------------------

IF OBJECT_ID(N'[dbo].[QRTZ_JOB_DETAILS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_JOB_DETAILS] (
        [SCHED_NAME]        nvarchar(120) NOT NULL,
        [JOB_NAME]          nvarchar(150) NOT NULL,
        [JOB_GROUP]         nvarchar(150) NOT NULL,
        [DESCRIPTION]       nvarchar(250) NULL,
        [JOB_CLASS_NAME]    nvarchar(250) NOT NULL,
        [IS_DURABLE]        bit           NOT NULL,
        [IS_NONCONCURRENT]  bit           NOT NULL,
        [IS_UPDATE_DATA]    bit           NOT NULL,
        [REQUESTS_RECOVERY] bit           NOT NULL,
        [JOB_DATA]          varbinary(max) NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_TRIGGERS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_TRIGGERS] (
        [SCHED_NAME]                   nvarchar(120) NOT NULL,
        [TRIGGER_NAME]                 nvarchar(150) NOT NULL,
        [TRIGGER_GROUP]                nvarchar(150) NOT NULL,
        [JOB_NAME]                     nvarchar(150) NOT NULL,
        [JOB_GROUP]                    nvarchar(150) NOT NULL,
        [DESCRIPTION]                  nvarchar(250) NULL,
        [NEXT_FIRE_TIME]               bigint        NULL,
        [PREV_FIRE_TIME]               bigint        NULL,
        [PRIORITY]                     int           NULL,
        [TRIGGER_STATE]                nvarchar(16)  NOT NULL,
        [TRIGGER_TYPE]                 nvarchar(8)   NOT NULL,
        [START_TIME]                   bigint        NOT NULL,
        [END_TIME]                     bigint        NULL,
        [CALENDAR_NAME]                nvarchar(200) NULL,
        [MISFIRE_INSTR]                int           NULL,
        [MISFIRE_ORIG_FIRE_TIME]       bigint        NULL,
        [EXECUTION_GROUP]              nvarchar(200) NULL,
        [PREFERRED_NODE]               nvarchar(200) NULL,
        [PREFERRED_NODE_AUTO]          bit           NOT NULL DEFAULT 0,
        [RETRY_POLICY]                 nvarchar(250) NULL,
        [RETRY_ATTEMPT]                int           NULL,
        [CONTINUES_TRIGGER_NAME]       nvarchar(150) NULL,
        [CONTINUES_TRIGGER_GROUP]      nvarchar(150) NULL,
        [CONTINUATION_CONDITION]       int           NULL,
        [JOB_DATA]                     varbinary(max) NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_CRON_TRIGGERS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_CRON_TRIGGERS] (
        [SCHED_NAME]      nvarchar(120) NOT NULL,
        [TRIGGER_NAME]    nvarchar(150) NOT NULL,
        [TRIGGER_GROUP]   nvarchar(150) NOT NULL,
        [CRON_EXPRESSION] nvarchar(120) NOT NULL,
        [TIME_ZONE_ID]    nvarchar(80)  NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_SIMPLE_TRIGGERS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_SIMPLE_TRIGGERS] (
        [SCHED_NAME]      nvarchar(120) NOT NULL,
        [TRIGGER_NAME]    nvarchar(150) NOT NULL,
        [TRIGGER_GROUP]   nvarchar(150) NOT NULL,
        [REPEAT_COUNT]    int           NOT NULL,
        [REPEAT_INTERVAL] bigint        NOT NULL,
        [TIMES_TRIGGERED] int           NOT NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_SIMPROP_TRIGGERS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_SIMPROP_TRIGGERS] (
        [SCHED_NAME]    nvarchar(120) NOT NULL,
        [TRIGGER_NAME]  nvarchar(150) NOT NULL,
        [TRIGGER_GROUP] nvarchar(150) NOT NULL,
        [STR_PROP_1]    nvarchar(512) NULL,
        [STR_PROP_2]    nvarchar(512) NULL,
        [STR_PROP_3]    nvarchar(512) NULL,
        [INT_PROP_1]    int           NULL,
        [INT_PROP_2]    int           NULL,
        [LONG_PROP_1]   bigint        NULL,
        [LONG_PROP_2]   bigint        NULL,
        [DEC_PROP_1]    numeric(13,4) NULL,
        [DEC_PROP_2]    numeric(13,4) NULL,
        [BOOL_PROP_1]   bit           NULL,
        [BOOL_PROP_2]   bit           NULL,
        [TIME_ZONE_ID]  nvarchar(80)  NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_BLOB_TRIGGERS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_BLOB_TRIGGERS] (
        [SCHED_NAME]    nvarchar(120) NOT NULL,
        [TRIGGER_NAME]  nvarchar(150) NOT NULL,
        [TRIGGER_GROUP] nvarchar(150) NOT NULL,
        [BLOB_DATA]     varbinary(max) NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_CALENDARS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_CALENDARS] (
        [SCHED_NAME]    nvarchar(120) NOT NULL,
        [CALENDAR_NAME] nvarchar(200) NOT NULL,
        [CALENDAR]      varbinary(max) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_FIRED_TRIGGERS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_FIRED_TRIGGERS] (
        [SCHED_NAME]        nvarchar(120) NOT NULL,
        [ENTRY_ID]          nvarchar(140) NOT NULL,
        [TRIGGER_NAME]      nvarchar(150) NOT NULL,
        [TRIGGER_GROUP]     nvarchar(150) NOT NULL,
        [INSTANCE_NAME]     nvarchar(200) NOT NULL,
        [FIRED_TIME]        bigint        NOT NULL,
        [SCHED_TIME]        bigint        NOT NULL,
        [PRIORITY]          int           NOT NULL,
        [STATE]             nvarchar(16)  NOT NULL,
        [JOB_NAME]          nvarchar(150) NULL,
        [JOB_GROUP]         nvarchar(150) NULL,
        [IS_NONCONCURRENT]  bit           NULL,
        [REQUESTS_RECOVERY] bit           NULL,
        [EXECUTION_GROUP]   nvarchar(200) NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_PAUSED_TRIGGER_GRPS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_PAUSED_TRIGGER_GRPS] (
        [SCHED_NAME]    nvarchar(120) NOT NULL,
        [TRIGGER_GROUP] nvarchar(150) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_PAUSED_JOB_GRPS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_PAUSED_JOB_GRPS] (
        [SCHED_NAME] nvarchar(120) NOT NULL,
        [JOB_GROUP]  nvarchar(150) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_SCHEDULER_STATE]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_SCHEDULER_STATE] (
        [SCHED_NAME]        nvarchar(120) NOT NULL,
        [INSTANCE_NAME]     nvarchar(200) NOT NULL,
        [LAST_CHECKIN_TIME] bigint        NOT NULL,
        [CHECKIN_INTERVAL]  bigint        NOT NULL
    );
END;
GO

IF OBJECT_ID(N'[dbo].[QRTZ_LOCKS]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QRTZ_LOCKS] (
        [SCHED_NAME] nvarchar(120) NOT NULL,
        [LOCK_NAME]  nvarchar(40)  NOT NULL
    );
END;
GO

-- ---------------------------------------------------------------------------
-- 2. 主键
-- ---------------------------------------------------------------------------

IF OBJECT_ID(N'[dbo].[PK_QRTZ_JOB_DETAILS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_JOB_DETAILS] ADD CONSTRAINT [PK_QRTZ_JOB_DETAILS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [JOB_NAME], [JOB_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_TRIGGERS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_TRIGGERS] ADD CONSTRAINT [PK_QRTZ_TRIGGERS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_CRON_TRIGGERS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_CRON_TRIGGERS] ADD CONSTRAINT [PK_QRTZ_CRON_TRIGGERS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_SIMPLE_TRIGGERS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_SIMPLE_TRIGGERS] ADD CONSTRAINT [PK_QRTZ_SIMPLE_TRIGGERS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_SIMPROP_TRIGGERS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_SIMPROP_TRIGGERS] ADD CONSTRAINT [PK_QRTZ_SIMPROP_TRIGGERS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_BLOB_TRIGGERS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_BLOB_TRIGGERS] ADD CONSTRAINT [PK_QRTZ_BLOB_TRIGGERS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_CALENDARS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_CALENDARS] ADD CONSTRAINT [PK_QRTZ_CALENDARS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [CALENDAR_NAME]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_FIRED_TRIGGERS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_FIRED_TRIGGERS] ADD CONSTRAINT [PK_QRTZ_FIRED_TRIGGERS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [ENTRY_ID]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_PAUSED_TRIGGER_GRPS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_PAUSED_TRIGGER_GRPS] ADD CONSTRAINT [PK_QRTZ_PAUSED_TRIGGER_GRPS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [TRIGGER_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_PAUSED_JOB_GRPS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_PAUSED_JOB_GRPS] ADD CONSTRAINT [PK_QRTZ_PAUSED_JOB_GRPS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [JOB_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_SCHEDULER_STATE]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_SCHEDULER_STATE] ADD CONSTRAINT [PK_QRTZ_SCHEDULER_STATE]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [INSTANCE_NAME]);
END;
GO

IF OBJECT_ID(N'[dbo].[PK_QRTZ_LOCKS]', N'PK') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_LOCKS] ADD CONSTRAINT [PK_QRTZ_LOCKS]
        PRIMARY KEY CLUSTERED ([SCHED_NAME], [LOCK_NAME]);
END;
GO

-- ---------------------------------------------------------------------------
-- 3. 外键（依赖 QRTZ_TRIGGERS / QRTZ_JOB_DETAILS，须在其建表与主键之后）
-- ---------------------------------------------------------------------------

IF OBJECT_ID(N'[dbo].[FK_QRTZ_TRIGGERS_QRTZ_JOB_DETAILS]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_TRIGGERS] ADD CONSTRAINT [FK_QRTZ_TRIGGERS_QRTZ_JOB_DETAILS]
        FOREIGN KEY ([SCHED_NAME], [JOB_NAME], [JOB_GROUP])
        REFERENCES [dbo].[QRTZ_JOB_DETAILS] ([SCHED_NAME], [JOB_NAME], [JOB_GROUP]);
END;
GO

IF OBJECT_ID(N'[dbo].[FK_QRTZ_CRON_TRIGGERS_QRTZ_TRIGGERS]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_CRON_TRIGGERS] ADD CONSTRAINT [FK_QRTZ_CRON_TRIGGERS_QRTZ_TRIGGERS]
        FOREIGN KEY ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        REFERENCES [dbo].[QRTZ_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        ON DELETE CASCADE;
END;
GO

IF OBJECT_ID(N'[dbo].[FK_QRTZ_SIMPLE_TRIGGERS_QRTZ_TRIGGERS]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_SIMPLE_TRIGGERS] ADD CONSTRAINT [FK_QRTZ_SIMPLE_TRIGGERS_QRTZ_TRIGGERS]
        FOREIGN KEY ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        REFERENCES [dbo].[QRTZ_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        ON DELETE CASCADE;
END;
GO

IF OBJECT_ID(N'[dbo].[FK_QRTZ_SIMPROP_TRIGGERS_QRTZ_TRIGGERS]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[QRTZ_SIMPROP_TRIGGERS] ADD CONSTRAINT [FK_QRTZ_SIMPROP_TRIGGERS_QRTZ_TRIGGERS]
        FOREIGN KEY ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        REFERENCES [dbo].[QRTZ_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP])
        ON DELETE CASCADE;
END;
GO
