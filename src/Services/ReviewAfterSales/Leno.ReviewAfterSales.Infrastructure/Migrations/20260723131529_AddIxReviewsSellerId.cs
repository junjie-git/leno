using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leno.ReviewAfterSales.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 补充 reviews.seller_id 索引（含 Include 列 created_at/rating），
    /// 使用 SQL Server ONLINE=ON 在线创建避免锁表，FILLFACTOR=90 预留页空间减少页分裂。
    /// 同时顺带补齐 outbox_messages.schema_version 字段（ModelSnapshot 与数据库预存不一致）。
    /// </summary>
    public partial class AddIxReviewsSellerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // outbox_messages 表：补齐 schema_version 字段（默认值 1，对应初始 schema 版本）。
            migrationBuilder.AddColumn<int>(
                name: "schema_version",
                table: "outbox_messages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // reviews 表：在线创建 seller_id 索引，INCLUDE created_at/rating 形成覆盖索引，
            // 避免卖家后台评价列表查询回表聚簇索引。
            // WITH (ONLINE = ON) 在线创建避免锁表（SQL Server Enterprise 版支持）；
            // FILLFACTOR = 90 预留 10% 页空间，减少高频写入场景的页分裂。
            migrationBuilder.Sql(
                @"CREATE INDEX ix_reviews_seller_id
                  ON reviews (seller_id)
                  INCLUDE (created_at, rating)
                  WITH (ONLINE = ON, FILLFACTOR = 90);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：先删索引再删列，避免索引依赖列时的冲突。
            migrationBuilder.Sql("DROP INDEX ix_reviews_seller_id ON reviews;");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_messages");
        }
    }
}
