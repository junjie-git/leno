using Leno.Points.Domain.Aggregates.PointsFlow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Points.Infrastructure.Configurations;

/// <summary>
/// PointsFlow 积分流水实体的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class PointsFlowConfiguration : IEntityTypeConfiguration<PointsFlow>
{
    public void Configure(EntityTypeBuilder<PointsFlow> builder)
    {
        builder.ToTable("points_flows");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.AccountId).HasColumnName("account_id");
        builder.Property(f => f.TxType).HasColumnName("tx_type").HasConversion<int>();
        builder.Property(f => f.Amount).HasColumnName("amount");
        builder.Property(f => f.BalanceAfter).HasColumnName("balance_after");
        builder.Property(f => f.Source).HasColumnName("source").HasConversion<int>();
        builder.Property(f => f.ReferenceId).HasColumnName("reference_id");
        builder.Property(f => f.Reason).HasColumnName("reason").HasMaxLength(512);
        builder.Property(f => f.OccurredAt).HasColumnName("occurred_at");

        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(f => new { f.AccountId, f.OccurredAt }).HasDatabaseName("ix_points_flows_account_occurred");
    }
}
