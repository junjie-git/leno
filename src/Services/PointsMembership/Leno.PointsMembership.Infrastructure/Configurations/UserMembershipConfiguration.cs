using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// UserMembership 用户会员权益聚合根的 EF Core 映射配置（snake_case）。
/// 按用户标识与订单标识建立索引，分别支持查询当前权益与支付回调激活。
/// </summary>
public sealed class UserMembershipConfiguration : IEntityTypeConfiguration<UserMembership>
{
    public void Configure(EntityTypeBuilder<UserMembership> builder)
    {
        builder.ToTable("user_memberships");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.UserId).HasColumnName("user_id");
        builder.Property(u => u.PackageId).HasColumnName("package_id");
        builder.Property(u => u.Level).HasColumnName("level");
        builder.Property(u => u.StartTime).HasColumnName("start_time");
        builder.Property(u => u.EndTime).HasColumnName("end_time");
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(u => u.OrderId).HasColumnName("order_id");

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // PM-M04 修复：RowVersion 乐观锁，并发更新时由数据库检测冲突并抛 DbUpdateConcurrencyException
        builder.Property(u => u.RowVersion).HasColumnName("row_version").IsRowVersion();

        builder.HasIndex(u => u.UserId).HasDatabaseName("ix_user_memberships_user_id");
        builder.HasIndex(u => u.OrderId).HasDatabaseName("ix_user_memberships_order_id");
    }
}
