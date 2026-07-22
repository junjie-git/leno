using Leno.Payment.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Payment.Infrastructure.Configurations;

/// <summary>
/// 对账差异聚合 EF Core 配置。
/// </summary>
public sealed class ReconciliationDiffConfiguration : IEntityTypeConfiguration<ReconciliationDiff>
{
    public void Configure(EntityTypeBuilder<ReconciliationDiff> builder)
    {
        // P1-14：表名改为 snake_case，与 payment_orders / refund_orders 等保持一致；
        // Channel/DiffType/Status 枚举由 HasConversion<string>() 改为 HasConversion<int>()，
        // 与 PaymentOrderConfiguration / RefundOrderConfiguration 中同枚举存储类型对齐。
        builder.ToTable("reconciliation_diffs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BillDate).IsRequired();
        builder.Property(x => x.Channel).IsRequired().HasConversion<int>();
        builder.Property(x => x.DiffType).IsRequired().HasConversion<int>();
        builder.Property(x => x.ChannelTransactionNo).HasMaxLength(128);
        builder.Property(x => x.ChannelAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.SystemTransactionNo).HasMaxLength(128);
        builder.Property(x => x.SystemAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Remark).HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasConversion<int>();

        builder.HasIndex(x => x.BillDate);
        builder.HasIndex(x => new { x.BillDate, x.Channel });
    }
}