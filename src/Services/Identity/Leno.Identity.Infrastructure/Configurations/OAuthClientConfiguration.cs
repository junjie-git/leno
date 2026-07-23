using System.Text.Json;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Leno.Identity.Infrastructure.Configurations;

/// <summary>
/// OAuthClient 聚合根的 EF Core 映射配置。
/// Provider 唯一索引，ClientSecret 以 ciphertext 存储。
/// 3.7 OAuth/SSO 通用化：扩展 ProviderType / DiscoveryUrl / Scopes / ClaimMappings 字段映射，
/// Scopes 与 ClaimMappings 以 JSON 列持久化（避免单独建表）。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>
{
    /// <summary>JSON 序列化选项，使用 Web 默认（驼峰命名）保持一致性。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Scopes 数组 ↔ JSON 字符串双向转换器。</summary>
    private static readonly ValueConverter<string[], string> ScopesConverter = new(
        v => JsonSerializer.Serialize(v, JsonOptions),
        v => JsonSerializer.Deserialize<string[]>(v, JsonOptions) ?? Array.Empty<string>());

    /// <summary>ClaimMappings 列表 ↔ JSON 字符串双向转换器。</summary>
    private static readonly ValueConverter<List<ClaimMapping>, string> ClaimMappingsConverter = new(
        v => JsonSerializer.Serialize(v, JsonOptions),
        v => JsonSerializer.Deserialize<List<ClaimMapping>>(v, JsonOptions) ?? new List<ClaimMapping>());

    public void Configure(EntityTypeBuilder<OAuthClient> builder)
    {
        builder.ToTable("oauth_clients");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.Provider).HasColumnName("provider").HasMaxLength(32).IsRequired();
        builder.Property(o => o.ProviderType).HasColumnName("provider_type").HasMaxLength(16).IsRequired();
        builder.Property(o => o.DiscoveryUrl).HasColumnName("discovery_url").HasMaxLength(512);
        builder.Property(o => o.ClientId).HasColumnName("client_id").HasMaxLength(256).IsRequired();
        builder.Property(o => o.ClientSecret).HasColumnName("client_secret").HasMaxLength(512).IsRequired();
        builder.Property(o => o.RedirectUri).HasColumnName("redirect_uri").HasMaxLength(512).IsRequired();
        builder.Property(o => o.Enabled).HasColumnName("enabled").IsRequired();

        // Scopes 与 ClaimMappings 以 JSON 列持久化（nvarchar(max)）
        // SetAfterSaveBehavior=Save 强制每次 Update 都回写，避免引用类型未变更检测导致跳过
        builder.Property(o => o.Scopes)
            .HasColumnName("scopes")
            .HasColumnType("nvarchar(max)")
            .HasConversion(ScopesConverter)
            .Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Save);

        builder.Property(o => o.ClaimMappings)
            .HasColumnName("claim_mappings")
            .HasColumnType("nvarchar(max)")
            .HasConversion(ClaimMappingsConverter)
            .Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Save);

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.HasIndex(o => o.Provider).HasDatabaseName("ix_oauth_clients_provider").IsUnique();
    }
}
