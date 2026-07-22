using Leno.Payment.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Payment.Infrastructure.Configurations;

/// <summary>
/// PaymentOrder 支付单聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ToTable("payment_orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.OutTradeNo).HasColumnName("out_trade_no").HasMaxLength(64).IsRequired();
        builder.Property(o => o.OrderId).HasColumnName("order_id");
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.Amount).HasColumnName("amount");
        builder.Property(o => o.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
        builder.Property(o => o.Channel).HasColumnName("channel").HasConversion<int>();
        builder.Property(o => o.ChannelTradeNo).HasColumnName("channel_trade_no").HasMaxLength(128);
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(o => o.PrepayId).HasColumnName("prepay_id").HasMaxLength(128);
        builder.Property(o => o.CodeUrl).HasColumnName("code_url").HasMaxLength(512);
        builder.Property(o => o.H5Url).HasColumnName("h5_url").HasMaxLength(512);
        builder.Property(o => o.ExpireAt).HasColumnName("expire_at");
        builder.Property(o => o.PaidAt).HasColumnName("paid_at");
        builder.Property(o => o.FailReason).HasColumnName("fail_reason").HasMaxLength(512);

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 乐观并发令牌：EF Core 在 UPDATE 时 WHERE row_version = @old_row_version，
        // 并发更新抛出 DbUpdateConcurrencyException，防止异步通知与补偿任务覆盖。
        builder.Property(o => o.RowVersion).HasColumnName("row_version").IsRowVersion();

        builder.HasIndex(o => o.OutTradeNo).IsUnique().HasDatabaseName("ix_payment_orders_out_trade_no");
        builder.HasIndex(o => o.OrderId).HasDatabaseName("ix_payment_orders_order_id");
        builder.HasIndex(o => o.UserId).HasDatabaseName("ix_payment_orders_user_id");
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_payment_orders_status");
    }
}
