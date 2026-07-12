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
        builder.ToTable("ReconciliationDiffs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BillDate).IsRequired();
        builder.Property(x => x.Channel).IsRequired().HasConversion<string>();
        builder.Property(x => x.DiffType).IsRequired().HasConversion<string>();
        builder.Property(x => x.ChannelTransactionNo).HasMaxLength(128);
        builder.Property(x => x.ChannelAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.SystemTransactionNo).HasMaxLength(128);
        builder.Property(x => x.SystemAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Remark).HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>();

        builder.HasIndex(x => x.BillDate);
        builder.HasIndex(x => new { x.BillDate, x.Channel });
    }
}