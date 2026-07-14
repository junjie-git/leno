using Leno.Payment.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Payment.Infrastructure.Configurations;

/// <summary>
/// PaymentChannelConfig 支付渠道配置聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class PaymentChannelConfigConfiguration : IEntityTypeConfiguration<PaymentChannelConfig>
{
    public void Configure(EntityTypeBuilder<PaymentChannelConfig> builder)
    {
        builder.ToTable("payment_channel_configs");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Channel).HasColumnName("channel").HasConversion<int>().IsRequired();
        builder.Property(c => c.ConfigName).HasColumnName("config_name").HasMaxLength(128).IsRequired();
        builder.Property(c => c.ConfigValue).HasColumnName("config_value").HasMaxLength(4096).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(c => c.Enabled).HasColumnName("enabled").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(c => c.Channel).HasDatabaseName("ix_payment_channel_configs_channel");
        builder.HasIndex(c => new { c.Channel, c.ConfigName }).IsUnique().HasDatabaseName("ix_payment_channel_configs_channel_name");
    }
}