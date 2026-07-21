using Leno.Promotion.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Promotion.Infrastructure.Configurations;

/// <summary>
/// 秒杀预占记录 EF Core 配置。
/// </summary>
public sealed class SeckillPreOccupationRecordConfiguration : IEntityTypeConfiguration<SeckillPreOccupationRecord>
{
    public void Configure(EntityTypeBuilder<SeckillPreOccupationRecord> builder)
    {
        builder.ToTable("seckill_pre_occupation_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ActivityId).IsRequired();
        builder.Property(r => r.SkuId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.OrderId).IsRequired();
        builder.Property(r => r.Quantity).IsRequired();
        builder.Property(r => r.PreOccupiedAt).IsRequired();
        builder.Property(r => r.IsFulfilled).IsRequired();
        builder.Property(r => r.FulfilledAt);
        builder.Property(r => r.IsRolledBack).IsRequired();
        builder.Property(r => r.RolledBackAt);

        builder.HasIndex(r => r.OrderId).IsUnique();
        builder.HasIndex(r => new { r.IsFulfilled, r.IsRolledBack, r.PreOccupiedAt });
    }
}