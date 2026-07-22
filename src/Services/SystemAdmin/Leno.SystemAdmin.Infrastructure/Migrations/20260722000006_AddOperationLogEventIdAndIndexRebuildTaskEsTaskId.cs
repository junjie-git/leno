using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SystemAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 补齐 SystemAdmin BC 缺失的 schema 变更：
    /// 1. operation_logs 表新增 event_id 列（uniqueidentifier，可空），
    ///    用于来源集成事件标识幂等去重；
    /// 2. operation_logs 新建 ix_operation_logs_event_id 唯一过滤索引
    ///    （仅当 event_id IS NOT NULL 时唯一），保证同事件不重复落日志；
    /// 3. index_rebuild_tasks 表新增 es_task_id 列（nvarchar(256)，可空），
    ///    用于关联底层搜索引擎（如 Elasticsearch）返回的任务标识。
    /// </summary>
    public partial class AddOperationLogEventIdAndIndexRebuildTaskEsTaskId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. operation_logs 新增 event_id 列（uniqueidentifier，可空）
            // OperationLog.EventId 为 Guid?，EF Core 默认映射为 uniqueidentifier
            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                table: "operation_logs",
                type: "uniqueidentifier",
                nullable: true);

            // 2. 新建 ix_operation_logs_event_id 唯一过滤索引
            // SQL Server 中可空列的唯一索引必须显式过滤 NULL，否则多条 NULL 记录会冲突
            migrationBuilder.CreateIndex(
                name: "ix_operation_logs_event_id",
                table: "operation_logs",
                column: "event_id",
                unique: true,
                filter: "[event_id] IS NOT NULL");

            // 3. index_rebuild_tasks 新增 es_task_id 列（nvarchar(256)，可空）
            // IndexRebuildTaskConfiguration 已配置 HasMaxLength(256)
            migrationBuilder.AddColumn<string>(
                name: "es_task_id",
                table: "index_rebuild_tasks",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 删除 ix_operation_logs_event_id 唯一索引
            migrationBuilder.DropIndex(
                name: "ix_operation_logs_event_id",
                table: "operation_logs");

            // 删除 operation_logs.event_id 列
            migrationBuilder.DropColumn(
                name: "event_id",
                table: "operation_logs");

            // 删除 index_rebuild_tasks.es_task_id 列
            migrationBuilder.DropColumn(
                name: "es_task_id",
                table: "index_rebuild_tasks");
        }
    }
}
