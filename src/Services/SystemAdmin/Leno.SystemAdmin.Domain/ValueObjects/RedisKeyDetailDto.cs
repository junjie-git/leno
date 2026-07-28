namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>Redis key 详情（含 value 内容，大 key 截断）。</summary>
public sealed class RedisKeyDetailDto
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Size { get; set; }
    public int Ttl { get; set; }
    public string Value { get; set; } = string.Empty;   // JSON 序列化后的值
    public bool Truncated { get; set; }                  // value 是否被截断（超 1MB）
}
