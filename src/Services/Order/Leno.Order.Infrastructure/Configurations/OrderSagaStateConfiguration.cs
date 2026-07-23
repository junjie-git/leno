using Leno.Order.Application.Sagas.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Order.Infrastructure.Configurations;

/// <summary>
/// <see cref="OrderSagaState"/> EF Core 映射配置（snake_case），持久化到 order_saga_states 表。
/// 主键 CorrelationId（= OrderId），row_version 列实现乐观锁（IVersionedSaga.Version）。
/// 索引：current_state 用于按状态统计 Saga 分布（Prometheus 指标 order_saga_state_total{state="..."}）。
/// </summary>
public sealed class OrderSagaStateConfiguration : IEntityTypeConfiguration<OrderSagaState>
{
    public void Configure(EntityTypeBuilder<OrderSagaState> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("order_saga_states");
        builder.HasKey(s => s.CorrelationId);

        builder.Property(s => s.CorrelationId)
            .HasColumnName("correlation_id")
            .HasColumnType("uniqueidentifier");

        builder.Property(s => s.CurrentState)
            .HasColumnName("current_state")
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)")
            .IsRequired();

        builder.Property(s => s.OrderId)
            .HasColumnName("order_id")
            .HasColumnType("uniqueidentifier");

        // UserId 在 Saga 内为 Guid（与 Order BC 一致），按计划 §5.1.2 schema 列类型为 bigint，
        // 但 Order BC 内 UserId 一直是 Guid，与 PaymentSucceededEvent.UserId 一致；此处保持 Guid 与 BC 既有不变量对齐。
        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uniqueidentifier");

        builder.Property(s => s.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.Currency)
            .HasColumnName("currency")
            .HasMaxLength(8)
            .HasColumnType("nvarchar(8)")
            .IsRequired();

        builder.Property(s => s.ItemsJson)
            .HasColumnName("items_json")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(s => s.StockReservationIdsJson)
            .HasColumnName("stock_reservation_ids_json")
            .HasColumnType("nvarchar(max)");

        builder.Property(s => s.PointsFrozenAmount)
            .HasColumnName("points_frozen_amount")
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.PaymentId)
            .HasColumnName("payment_id")
            .HasColumnType("uniqueidentifier");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2");

        // 乐观锁 rowversion：实现 IVersionedSaga.Version，MassTransit EF Saga 据此检测并发冲突
        builder.Property(s => s.Version)
            .HasColumnName("row_version")
            .IsRowVersion()
            .IsRequired();

        // 按状态查询索引：用于 Prometheus 指标 order_saga_state_total{state="..."} 统计
        builder.HasIndex(s => s.CurrentState)
            .HasDatabaseName("ix_order_saga_states_current_state");
    }
}
