using Leno.UserCenter.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.UserCenter.Infrastructure.Configurations;

/// <summary>
/// Address 聚合根的 EF Core 映射配置。表名 snake_case；UserId 外键索引。
/// 地址软删除基于 AddressStatus，不使用 ISoftDeletable 全局过滤器。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.RecipientName).HasColumnName("recipient_name").HasMaxLength(32).IsRequired();
        builder.Property(a => a.RecipientPhone).HasColumnName("recipient_phone").HasMaxLength(20).IsRequired();
        builder.Property(a => a.Province).HasColumnName("province").HasMaxLength(64).IsRequired();
        builder.Property(a => a.City).HasColumnName("city").HasMaxLength(64).IsRequired();
        builder.Property(a => a.District).HasColumnName("district").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Detail).HasColumnName("detail").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Tag).HasColumnName("tag").HasMaxLength(8);
        builder.Property(a => a.IsDefault).HasColumnName("is_default");
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_addresses_user_id");
        // 默认地址唯一不变量：每用户最多一条 is_default = 1，由唯一过滤索引在数据库层兜底
        // 防止 AddressAppService.SetDefaultAsync 并发读改写场景下出现多条默认地址
        builder.HasIndex(a => new { a.UserId, a.IsDefault })
            .HasDatabaseName("ix_addresses_user_default")
            .IsUnique()
            .HasFilter("[is_default] = 1");
    }
}
