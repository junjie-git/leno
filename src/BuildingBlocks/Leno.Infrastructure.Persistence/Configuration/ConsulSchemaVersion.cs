using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Leno.Infrastructure.Configuration;

/// <summary>
/// Consul 配置 Schema 版本元数据（阶段四 4.6 步骤6）。
/// 记录配置版本号、Schema 哈希、应用时间、应用者，支持配置变更可追溯、可回滚。
/// </summary>
public sealed class ConsulSchemaVersion : IEquatable<ConsulSchemaVersion>
{
    /// <summary>配置 Schema 版本号，单调递增。</summary>
    public int Version { get; init; }

    /// <summary>配置 JSON 内容的哈希（SHA-256），用于校验配置是否被篡改。</summary>
    public string SchemaHash { get; init; } = string.Empty;

    /// <summary>配置应用时间（UTC）。</summary>
    public DateTime AppliedAt { get; init; }

    /// <summary>配置应用者（服务名或操作人）。</summary>
    public string AppliedBy { get; init; } = string.Empty;

    /// <summary>计算配置 JSON 的 SHA-256 哈希。</summary>
    public static string ComputeSchemaHash(string configJson)
    {
        if (configJson is null)
        {
            throw new ArgumentNullException(nameof(configJson));
        }

        // 规范化：解析 JSON 后重新序列化，消除格式差异（空格、键顺序）
        string normalized;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            normalized = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (JsonException)
        {
            // 非 JSON 配置，直接哈希原始字符串
            normalized = configJson;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>创建当前配置的 Schema 版本快照。</summary>
    public static ConsulSchemaVersion Create(int version, string configJson, string appliedBy)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "版本号必须为正整数");
        }

        return new ConsulSchemaVersion
        {
            Version = version,
            SchemaHash = ComputeSchemaHash(configJson),
            AppliedAt = DateTime.UtcNow,
            AppliedBy = appliedBy ?? string.Empty
        };
    }

    public bool Equals(ConsulSchemaVersion? other)
    {
        return other is not null && Version == other.Version && SchemaHash == other.SchemaHash;
    }

    public override bool Equals(object? obj) => Equals(obj as ConsulSchemaVersion);

    public override int GetHashCode() => HashCode.Combine(Version, SchemaHash);

    public override string ToString()
        => $"v{Version} [{SchemaHash[..8]}] {AppliedAt:O} by {AppliedBy}";
}
