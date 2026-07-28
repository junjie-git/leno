namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>Redis key 摘要。</summary>
public sealed class RedisKeyDto
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Size { get; set; }
    public int Ttl { get; set; }   // -1 表示永不过期
}
