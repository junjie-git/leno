using Leno.SellerShop.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SellerShop.Infrastructure.Configurations;

/// <summary>
/// SellerProfile 聚合根的 EF Core 映射配置。
/// 表名 snake_case；UserId 唯一索引（一账号一档案）。
/// </summary>
public sealed class SellerProfileConfiguration : IEntityTypeConfiguration<SellerProfile>
{
    public void Configure(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.ToTable("seller_profiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.RealName).HasColumnName("real_name").HasMaxLength(32).IsRequired();
        builder.Property(p => p.IdCard).HasColumnName("id_card").HasMaxLength(18);
        builder.Property(p => p.BusinessLicenseNo).HasColumnName("business_license_no").HasMaxLength(32);
        builder.Property(p => p.BankAccount).HasColumnName("bank_account").HasMaxLength(64);
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(p => p.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(p => p.StatusReason).HasColumnName("status_reason").HasMaxLength(200);

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.HasIndex(p => p.UserId).HasDatabaseName("ix_seller_profiles_user_id").IsUnique();
        builder.HasIndex(p => p.Status).HasDatabaseName("ix_seller_profiles_status");
    }
}
