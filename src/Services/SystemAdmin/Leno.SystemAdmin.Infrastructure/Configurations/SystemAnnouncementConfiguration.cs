using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// SystemAnnouncement 系统公告的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class SystemAnnouncementConfiguration : IEntityTypeConfiguration<SystemAnnouncement>
{
    public void Configure(EntityTypeBuilder<SystemAnnouncement> builder)
    {
        builder.ToTable("system_announcements");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Content).HasColumnName("content").HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(a => a.Type).HasColumnName("type").HasConversion<int>();
        builder.Property(a => a.TargetAudience).HasColumnName("target_audience").HasConversion<int>();
        builder.Property(a => a.PublishAt).HasColumnName("publish_at");
        builder.Property(a => a.ExpireAt).HasColumnName("expire_at");
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(a => a.Version).HasColumnName("version").IsRowVersion();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(a => a.Status).HasDatabaseName("ix_system_announcements_status");
    }
}
