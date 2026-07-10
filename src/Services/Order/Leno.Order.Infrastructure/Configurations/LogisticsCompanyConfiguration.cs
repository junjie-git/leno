using Leno.Order.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Order.Infrastructure.Configurations;

/// <summary>
/// LogisticsCompany 物流公司聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class LogisticsCompanyConfiguration : IEntityTypeConfiguration<LogisticsCompany>
{
    public void Configure(EntityTypeBuilder<LogisticsCompany> builder)
    {
        builder.ToTable("logistics_companies");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(l => l.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(l => l.ServicePhone).HasColumnName("service_phone").HasMaxLength(32);
        builder.Property(l => l.SupportTracking).HasColumnName("support_tracking");
        builder.Property(l => l.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(l => l.Version).HasColumnName("version").IsRowVersion();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(l => l.Code).IsUnique().HasDatabaseName("ix_logistics_companies_code");
    }
}
