using System.Text.Json;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.ReviewAfterSales.Infrastructure.Configurations;

/// <summary>
/// AfterSales 售后单聚合根的 EF Core 映射配置（snake_case）。
/// 凭证图片集合 _images 序列化为 JSON 列；售后类型与状态以 int 持久化；Version 作 rowversion 乐观锁。
/// </summary>
public sealed class AfterSalesConfiguration : IEntityTypeConfiguration<AfterSales>
{
    public void Configure(EntityTypeBuilder<AfterSales> builder)
    {
        builder.ToTable("after_sales");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.OrderId).HasColumnName("order_id");
        builder.Property(a => a.OrderLineId).HasColumnName("order_line_id");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.SellerId).HasColumnName("seller_id");
        builder.Property(a => a.Type).HasColumnName("type").HasConversion<int>();
        builder.Property(a => a.ReasonCategory).HasColumnName("reason_category").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(a => a.RequestedAmount).HasColumnName("requested_amount");
        builder.Property(a => a.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
        builder.Property(a => a.ApprovedAmount).HasColumnName("approved_amount");
        builder.Property(a => a.RefundedAmount).HasColumnName("refunded_amount");
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(a => a.AppliedAt).HasColumnName("applied_at");
        builder.Property(a => a.ApprovedAt).HasColumnName("approved_at");
        builder.Property(a => a.ApproverId).HasColumnName("approver_id");
        builder.Property(a => a.RefundedAt).HasColumnName("refunded_at");
        builder.Property(a => a.ChannelRefundNo).HasColumnName("channel_refund_no").HasMaxLength(128);
        builder.Property(a => a.RejectReason).HasColumnName("reject_reason").HasMaxLength(200);
        builder.Property(a => a.FailReason).HasColumnName("fail_reason").HasMaxLength(512);
        builder.Property(a => a.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(a => a.CancelReason).HasColumnName("cancel_reason").HasMaxLength(200);

        builder.Property(a => a.Version).HasColumnName("version").IsRowVersion();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 凭证图片 URL 集合序列化为 JSON 列
        builder.Property(a => a.Images)
            .HasColumnName("images")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        builder.HasIndex(a => a.OrderId).HasDatabaseName("ix_after_sales_order_id");
        builder.HasIndex(a => a.SellerId).HasDatabaseName("ix_after_sales_seller_id");
        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_after_sales_user_id");
        builder.HasIndex(a => a.Status).HasDatabaseName("ix_after_sales_status");
    }
}
