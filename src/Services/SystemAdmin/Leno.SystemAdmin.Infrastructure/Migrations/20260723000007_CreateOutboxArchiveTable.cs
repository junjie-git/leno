using Leno.SystemAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SystemAdmin.Infrastructure.Migrations;

/// <inheritdoc />
/// <summary>
/// 任务 2.4.7：创建 outbox_messages_archive 归档表。
/// 表结构通过 <c>SELECT * INTO ... WHERE 1=0</c> 复制自 outbox_messages（仅 schema，不含数据），
/// 自动继承所有列定义（id/type/payload/occurred_at/processed_at/publishing_started_at/retry_count/error/status/schema_version）
/// 与可空性，但不含原表的索引/约束（仅保留聚簇索引 ix_outbox_archive_id）。
/// 由 OutboxArchivalBackgroundService 在每天 02:00 UTC 将 ProcessedAt 早于 7 天的已处理记录归档至本表。
/// 手写 SQL 迁移，不依赖 ModelSnapshot（与 OutboxMessage 实体配置无关联，独立归档表）。
/// </summary>
[DbContext(typeof(SystemAdminDbContext))]
[Migration("20260723000007_CreateOutboxArchiveTable")]
public partial class CreateOutboxArchiveTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. 通过 SELECT * INTO 复制 outbox_messages 的列结构到 outbox_messages_archive，
        //    WHERE 1=0 确保不复制任何数据。新表无索引/约束/默认值，仅保留列定义与可空性。
        migrationBuilder.Sql(
            @"SELECT * INTO outbox_messages_archive FROM outbox_messages WHERE 1 = 0;");

        // 2. 在归档表 id 列创建聚簇索引，加速按 ID 范围查询（归档批次查询/审计回溯）
        //    使用 SQL Server 在线创建索引（WITH (ONLINE = ON)）避免锁表，FILLFACTOR = 90 降低页分裂
        migrationBuilder.Sql(
            @"CREATE CLUSTERED INDEX ix_outbox_archive_id
              ON outbox_messages_archive (id)
              WITH (ONLINE = ON, FILLFACTOR = 90);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_messages_archive;");
    }
}
