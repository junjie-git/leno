using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.SystemAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// M-10：将 dead_letter_messages.original_message_id 索引改为唯一索引，
    /// 消除 RabbitMqDeadLetterManager.PersistDeadLetterCopyAsync 的 check-then-insert TOCTOU 竞态。
    /// 应用层捕获 DbUpdateException 判定为唯一约束冲突时按幂等处理。
    /// </summary>
    public partial class MakeDeadLetterOriginalMessageIdUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 删除原有非唯一索引，重建为唯一索引
            migrationBuilder.DropIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages",
                column: "original_message_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚为非唯一索引
            migrationBuilder.DropIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_messages_original_message_id",
                table: "dead_letter_messages",
                column: "original_message_id");
        }
    }
}
