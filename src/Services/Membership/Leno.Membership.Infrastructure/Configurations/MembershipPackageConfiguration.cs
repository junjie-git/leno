using Leno.Membership.Domain.Aggregates.MembershipPackage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MembershipPackageAggregate = Leno.Membership.Domain.Aggregates.MembershipPackage.MembershipPackage;

namespace Leno.Membership.Infrastructure.Configurations;

/// <summary>
/// MembershipPackage 会员套餐聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class MembershipPackageConfiguration : IEntityTypeConfiguration<MembershipPackageAggregate>
{
    public void Configure(EntityTypeBuilder<MembershipPackageAggregate> builder)
    {
        builder.ToTable("membership_packages");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(p => p.Level).HasColumnName("level");
        builder.Property(p => p.Price).HasColumnName("price").HasPrecision(18, 2);
        builder.Property(p => p.DurationDays).HasColumnName("duration_days");
        builder.Property(p => p.Benefits).HasColumnName("benefits");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(p => p.Level).HasDatabaseName("ix_membership_packages_level");
    }
}
