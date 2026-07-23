using Leno.Order.Application.ProcessManagers.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Order.Infrastructure.Configurations;

/// <summary>
/// <see cref="OrderPaymentProcessState"/> EF Core 映射配置（snake_case），持久化到 order_payment_processes 表。
/// 主键 ProcessId，row_version 列实现乐观锁（<see cref="OrderPaymentProcessState.RowVersion"/>）。
/// 索引：
/// <list type="bullet">
/// <item>order_id 唯一索引：保证同一订单仅一个进行中的支付流程（一对一约束）。</item>
/// <item>current_state 索引：用于按状态统计流程分布（Prometheus 指标 order_payment_process_state_total{state="..."}）。</item>
/// </list>
/// </summary>
public sealed class OrderPaymentProcessStateConfiguration : IEntityTypeConfiguration<OrderPaymentProcessState>
{
    public void Configure(EntityTypeBuilder<OrderPaymentProcessState> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("order_payment_processes");
        builder.HasKey(s => s.ProcessId);

        builder.Property(s => s.ProcessId)
            .HasColumnName("process_id")
            .HasColumnType("uniqueidentifier");

        builder.Property(s => s.OrderId)
            .HasColumnName("order_id")
            .HasColumnType("uniqueidentifier");

        builder.Property(s => s.PaymentId)
            .HasColumnName("payment_id")
            .HasColumnType("uniqueidentifier");

        builder.Property(s => s.CurrentState)
            .HasColumnName("current_state")
            .HasMaxLength(32)
            .HasColumnType("nvarchar(32)")
            .IsRequired();

        builder.Property(s => s.StockConfirmed)
            .HasColumnName("stock_confirmed")
            .HasColumnType("bit");

        builder.Property(s => s.PointsConfirmed)
            .HasColumnName("points_confirmed")
            .HasColumnType("bit");

        builder.Property(s => s.OrderMarkedPaid)
            .HasColumnName("order_marked_paid")
            .HasColumnType("bit");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2");

        // 乐观锁 rowversion：三个子任务可能并发回写状态，EF Core 据此检测并发冲突
        builder.Property(s => s.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion()
            .IsRequired();

        // 订单唯一索引：同一订单仅允许一个支付流程实例（避免重复创建）
        builder.HasIndex(s => s.OrderId)
            .IsUnique()
            .HasDatabaseName("ix_order_payment_processes_order_id");

        // 按状态查询索引：用于 Prometheus 指标 order_payment_process_state_total{state="..."} 统计
        builder.HasIndex(s => s.CurrentState)
            .HasDatabaseName("ix_order_payment_processes_current_state");
    }
}
