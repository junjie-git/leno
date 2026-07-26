using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// OutboxArchiveRecord 归档历史的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class OutboxArchiveRecordConfiguration : IEntityTypeConfiguration<OutboxArchiveRecord>
{
    public void Configure(EntityTypeBuilder<OutboxArchiveRecord> builder)
    {
        builder.ToTable("outbox_archive_records");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Context).HasColumnName("context").HasMaxLength(128).IsRequired();
        builder.Property(r => r.ArchivedCount).HasColumnName("archived_count").IsRequired();
        builder.Property(r => r.ArchivedBefore).HasColumnName("archived_before").IsRequired();
        builder.Property(r => r.ArchivedAt).HasColumnName("archived_at").IsRequired();
        builder.Property(r => r.ArchivedBy).HasColumnName("archived_by").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(r => r.Context).HasDatabaseName("ix_outbox_archive_records_context");
        builder.HasIndex(r => r.ArchivedAt).HasDatabaseName("ix_outbox_archive_records_archived_at");
    }
}
