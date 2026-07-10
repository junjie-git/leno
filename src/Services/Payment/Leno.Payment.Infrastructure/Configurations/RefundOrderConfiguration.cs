using Leno.Payment.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Payment.Infrastructure.Configurations;

/// <summary>
/// RefundOrder 退款单聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class RefundOrderConfiguration : IEntityTypeConfiguration<RefundOrder>
{
    public void Configure(EntityTypeBuilder<RefundOrder> builder)
    {
        builder.ToTable("refund_orders");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.OutRefundNo).HasColumnName("out_refund_no").HasMaxLength(64).IsRequired();
        builder.Property(r => r.OutTradeNo).HasColumnName("out_trade_no").HasMaxLength(64).IsRequired();
        builder.Property(r => r.PaymentId).HasColumnName("payment_id");
        builder.Property(r => r.OrderId).HasColumnName("order_id");
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.AfterSalesId).HasColumnName("after_sales_id");
        builder.Property(r => r.RefundAmount).HasColumnName("refund_amount");
        builder.Property(r => r.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
        builder.Property(r => r.Channel).HasColumnName("channel").HasConversion<int>();
        builder.Property(r => r.ChannelRefundNo).HasColumnName("channel_refund_no").HasMaxLength(128);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(r => r.RefundedAt).HasColumnName("refunded_at");
        builder.Property(r => r.FailReason).HasColumnName("fail_reason").HasMaxLength(512);

        builder.Property(r => r.Version).HasColumnName("version").IsRowVersion();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(r => r.OutRefundNo).IsUnique().HasDatabaseName("ix_refund_orders_out_refund_no");
        builder.HasIndex(r => r.PaymentId).HasDatabaseName("ix_refund_orders_payment_id");
        builder.HasIndex(r => r.OrderId).HasDatabaseName("ix_refund_orders_order_id");
        builder.HasIndex(r => r.AfterSalesId).HasDatabaseName("ix_refund_orders_after_sales_id");
    }
}
