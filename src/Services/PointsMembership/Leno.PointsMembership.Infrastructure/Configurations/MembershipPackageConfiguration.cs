using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// MembershipPackage 会员套餐聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class MembershipPackageConfiguration : IEntityTypeConfiguration<MembershipPackage>
{
    public void Configure(EntityTypeBuilder<MembershipPackage> builder)
    {
        builder.ToTable("membership_packages");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(p => p.Level).HasColumnName("level");
        builder.Property(p => p.Price).HasColumnName("price").HasPrecision(18, 2);
        builder.Property(p => p.DurationDays).HasColumnName("duration_days");
        builder.Property(p => p.Benefits).HasColumnName("benefits").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.Property(p => p.Version).HasColumnName("version").IsRowVersion();

        builder.HasIndex(p => p.Status).HasDatabaseName("ix_membership_packages_status");
    }
}
