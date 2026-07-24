using Leno.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// OutboxMessage 发件箱消息的 EF Core 映射配置（snake_case）。
/// 由 BaseDbContext.OnModelCreating 统一应用，各 BC 无需重复声明。
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.Type).HasColumnName("type").HasMaxLength(512).IsRequired();
        builder.Property(o => o.Payload).HasColumnName("payload").IsRequired();
        builder.Property(o => o.OccurredAt).HasColumnName("occurred_at");
        builder.Property(o => o.ProcessedAt).HasColumnName("processed_at");
        builder.Property(o => o.PublishingStartedAt).HasColumnName("publishing_started_at");
        builder.Property(o => o.RetryCount).HasColumnName("retry_count");
        builder.Property(o => o.Error).HasColumnName("error");
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(o => o.SchemaVersion)
            .HasColumnName("schema_version")
            .HasDefaultValue(1)
            .IsRequired();

        // 4.4 Outbox 分片发布器：聚合根 ID + 分片键
        builder.Property(o => o.AggregateRootId).HasColumnName("aggregate_root_id");
        builder.Property(o => o.ShardKey).HasColumnName("shard_key").HasDefaultValue(0).IsRequired();

        builder.HasIndex(o => o.Status).HasDatabaseName("ix_outbox_messages_status");

        // 4.4：分片键 + 处理状态复合索引，供 ShardedOutboxPublisher 按本实例分片号拉取 pending 消息
        builder.HasIndex(o => new { o.ShardKey, o.Status })
            .HasDatabaseName("ix_outbox_shard_status");
    }
}
