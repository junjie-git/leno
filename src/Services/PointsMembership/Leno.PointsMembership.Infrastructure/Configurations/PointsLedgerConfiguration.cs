using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// PointsLedger 积分流水实体的 EF Core 映射配置（snake_case）。
/// 流水按账户追加写入，外键引用积分账户但不级联删除，保留完整审计历史。
/// </summary>
public sealed class PointsLedgerConfiguration : IEntityTypeConfiguration<PointsLedger>
{
    public void Configure(EntityTypeBuilder<PointsLedger> builder)
    {
        builder.ToTable("points_ledgers");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.AccountId).HasColumnName("account_id");
        builder.Property(l => l.TxType).HasColumnName("tx_type").HasConversion<int>();
        builder.Property(l => l.Amount).HasColumnName("amount");
        builder.Property(l => l.BalanceAfter).HasColumnName("balance_after");
        builder.Property(l => l.Source).HasColumnName("source").HasConversion<int>();
        builder.Property(l => l.ReferenceId).HasColumnName("reference_id");
        builder.Property(l => l.Reason).HasColumnName("reason").HasMaxLength(256).IsRequired();
        builder.Property(l => l.OccurredAt).HasColumnName("occurred_at");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 流水引用积分账户，无反向导航集合，删除账户时禁止级联以保留历史。
        builder.HasOne<PointsAccount>()
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.AccountId, l.OccurredAt })
            .HasDatabaseName("ix_points_ledgers_account_id_occurred_at");
    }
}
