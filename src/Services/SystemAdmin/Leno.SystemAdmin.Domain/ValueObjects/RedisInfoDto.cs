namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>Redis INFO 命令解析结果。</summary>
public sealed class RedisInfoDto
{
    public string RedisVersion { get; set; } = string.Empty;
    public string RedisMode { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string ArchBits { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public int UptimeInDays { get; set; }
    public int ConnectedClients { get; set; }
    public string UsedMemoryHuman { get; set; } = string.Empty;
    public string UsedMemoryPeakHuman { get; set; } = string.Empty;
    public string MaxmemoryHuman { get; set; } = string.Empty;
    public double MemFragmentationRatio { get; set; }
    public long TotalConnectionsReceived { get; set; }
    public long TotalCommandsProcessed { get; set; }
    public long KeyspaceHits { get; set; }
    public long KeyspaceMisses { get; set; }
    public long EvictedKeys { get; set; }
}
