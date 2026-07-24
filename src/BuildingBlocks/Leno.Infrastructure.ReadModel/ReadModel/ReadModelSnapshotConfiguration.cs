using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// <see cref="ReadModelSnapshot"/> 的 EF Core 映射配置。
/// 映射到 <c>read_model_snapshots</c> 表，复合主键 (aggregate_id, version)，
/// 并为 (aggregate_type) 建立索引以支持按聚合类型列出快照。
/// </summary>
public sealed class ReadModelSnapshotConfiguration : IEntityTypeConfiguration<ReadModelSnapshot>
{
    public void Configure(EntityTypeBuilder<ReadModelSnapshot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("read_model_snapshots");

        builder.HasKey(s => new { s.AggregateId, s.Version });

        builder.Property(s => s.AggregateId)
            .HasColumnName("aggregate_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(s => s.AggregateType)
            .HasColumnName("aggregate_type")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(s => s.Version)
            .HasColumnName("version");

        builder.Property(s => s.StateJson)
            .HasColumnName("state_json")
            .IsRequired();

        builder.Property(s => s.TakenAt)
            .HasColumnName("taken_at");

        builder.HasIndex(s => s.AggregateType)
            .HasDatabaseName("ix_read_model_snapshots_aggregate_type");
    }
}
