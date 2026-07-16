using Leno.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// OutboxMessage 发件箱消息的 EF Core 映射配置（snake_case）。
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

        builder.HasIndex(o => o.Status).HasDatabaseName("ix_outbox_messages_status");
    }
}
