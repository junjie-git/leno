namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>Redis keyspace 信息。</summary>
public sealed class KeyspaceDto
{
    public int Db { get; set; }
    public int Keys { get; set; }
    public int Expires { get; set; }
    public int AvgTtl { get; set; }
}
