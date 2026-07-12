using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// DeadLetterMessage 死信消息的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class DeadLetterMessageConfiguration : IEntityTypeConfiguration<DeadLetterMessage>
{
    public void Configure(EntityTypeBuilder<DeadLetterMessage> builder)
    {
        builder.ToTable("dead_letter_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.OriginalMessageId).HasColumnName("original_message_id").HasMaxLength(128).IsRequired();
        builder.Property(m => m.SourceContext).HasColumnName("source_context").HasMaxLength(256).IsRequired();
        builder.Property(m => m.OriginalTopic).HasColumnName("original_topic").HasMaxLength(256).IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.Headers).HasColumnName("headers").HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.ErrorReason).HasColumnName("error_reason").HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(m => m.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
        builder.Property(m => m.DiscardReason).HasColumnName("discard_reason").HasMaxLength(1000);
        builder.Property(m => m.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at");

        builder.Property(m => m.Version).HasColumnName("version").IsRowVersion();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(m => m.OriginalMessageId).HasDatabaseName("ix_dead_letter_messages_original_message_id");
        builder.HasIndex(m => m.SourceContext).HasDatabaseName("ix_dead_letter_messages_source_context");
        builder.HasIndex(m => m.Status).HasDatabaseName("ix_dead_letter_messages_status");
    }
}