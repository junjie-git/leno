using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// CheckInRecord 签到记录聚合根的 EF Core 映射配置（snake_case）。
/// 按用户与签到日期建立唯一索引，防重复签到。
/// </summary>
public sealed class CheckInRecordConfiguration : IEntityTypeConfiguration<CheckInRecord>
{
    public void Configure(EntityTypeBuilder<CheckInRecord> builder)
    {
        builder.ToTable("check_in_records");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.CheckInDate).HasColumnName("check_in_date");
        builder.Property(r => r.ContinuousDays).HasColumnName("continuous_days");
        builder.Property(r => r.PointsAwarded).HasColumnName("points_awarded");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(r => new { r.UserId, r.CheckInDate })
            .IsUnique()
            .HasDatabaseName("ix_check_in_records_user_id_check_in_date");
    }
}
