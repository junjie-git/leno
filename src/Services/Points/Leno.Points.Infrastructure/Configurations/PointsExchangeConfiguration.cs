using Leno.Points.Domain.Aggregates.PointsExchange;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointsExchangeAggregate = Leno.Points.Domain.Aggregates.PointsExchange.PointsExchange;

namespace Leno.Points.Infrastructure.Configurations;

/// <summary>
/// PointsExchange 积分兑换聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class PointsExchangeConfiguration : IEntityTypeConfiguration<PointsExchangeAggregate>
{
    public void Configure(EntityTypeBuilder<PointsExchangeAggregate> builder)
    {
        builder.ToTable("points_exchanges");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.TargetId).HasColumnName("target_id");
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<int>();
        builder.Property(e => e.PointsRequired).HasColumnName("points_required");
        builder.Property(e => e.PointsAccountId).HasColumnName("points_account_id");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(e => e.RequestedAt).HasColumnName("requested_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(512);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_points_exchanges_user_id");
        builder.HasIndex(e => new { e.TargetId, e.UserId }).IsUnique().HasDatabaseName("ix_points_exchanges_target_user");
    }
}
