using Leno.UserAuth.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.UserAuth.Infrastructure.Configurations;

/// <summary>
/// OAuthClient 聚合根的 EF Core 映射配置。
/// Provider 唯一索引，ClientSecret 以 ciphertext 存储。
/// </summary>
public sealed class OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>
{
    public void Configure(EntityTypeBuilder<OAuthClient> builder)
    {
        builder.ToTable("oauth_clients");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.Provider).HasColumnName("provider").HasMaxLength(32).IsRequired();
        builder.Property(o => o.ClientId).HasColumnName("client_id").HasMaxLength(256).IsRequired();
        builder.Property(o => o.ClientSecret).HasColumnName("client_secret").HasMaxLength(512).IsRequired();
        builder.Property(o => o.RedirectUri).HasColumnName("redirect_uri").HasMaxLength(512).IsRequired();
        builder.Property(o => o.Enabled).HasColumnName("enabled").IsRequired();

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.Property(o => o.Version).HasColumnName("version").IsRowVersion();

        builder.HasIndex(o => o.Provider).HasDatabaseName("ix_oauth_clients_provider").IsUnique();
    }
}