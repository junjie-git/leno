using Leno.UserAuth.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.UserAuth.Infrastructure.Configurations;

/// <summary>
/// Address 聚合根的 EF Core 映射配置。表名 snake_case；UserId 外键索引。
/// 地址软删除基于 AddressStatus，不使用 ISoftDeletable 全局过滤器。
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

        builder.Property(a => a.Version).HasColumnName("version").IsRowVersion();

        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_addresses_user_id");
        builder.HasIndex(a => new { a.UserId, a.IsDefault }).HasDatabaseName("ix_addresses_user_default");
    }
}
