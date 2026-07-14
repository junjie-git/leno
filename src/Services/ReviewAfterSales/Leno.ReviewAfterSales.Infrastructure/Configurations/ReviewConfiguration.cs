using System.Text.Json;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.ReviewAfterSales.Infrastructure.Configurations;

/// <summary>
/// Review 评价聚合根的 EF Core 映射配置（snake_case）。
/// 图片集合 _images 序列化为 JSON 列；审核状态以 int 持久化；Version 作 rowversion 乐观锁。
/// </summary>
public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.OrderId).HasColumnName("order_id");
        builder.Property(r => r.OrderLineId).HasColumnName("order_line_id");
        builder.Property(r => r.SpuId).HasColumnName("spu_id");
        builder.Property(r => r.SkuId).HasColumnName("sku_id");
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.Rating).HasColumnName("rating");
        builder.Property(r => r.Content).HasColumnName("content").HasMaxLength(500).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(r => r.SellerReplyContent).HasColumnName("seller_reply_content").HasMaxLength(500);
        builder.Property(r => r.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(r => r.AuditedAt).HasColumnName("audited_at");
        builder.Property(r => r.AuditorId).HasColumnName("auditor_id");
        builder.Property(r => r.HiddenAt).HasColumnName("hidden_at");
        builder.Property(r => r.HiddenBy).HasColumnName("hidden_by");
        builder.Property(r => r.HideReason).HasColumnName("hide_reason").HasMaxLength(200);

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 图片 URL 集合序列化为 JSON 列
        builder.Property(r => r.Images)
            .HasColumnName("images")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        builder.HasIndex(r => r.SpuId).HasDatabaseName("ix_reviews_spu_id");
        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_reviews_user_id");
        builder.HasIndex(r => r.OrderLineId).IsUnique().HasDatabaseName("ix_reviews_order_line_id");
    }
}
