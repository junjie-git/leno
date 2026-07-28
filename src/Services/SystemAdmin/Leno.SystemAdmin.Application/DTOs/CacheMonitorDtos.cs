using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application.DTOs;

/// <summary>
/// 缓存 key 查询响应，对应前端 spec §3.6。
/// 直接复用领域层 <see cref="RedisKeyDto"/>。
/// </summary>
public sealed class CacheKeyQueryResultDto
{
    public List<RedisKeyDto> Items { get; set; } = [];

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

/// <summary>
/// 删除缓存 key 响应，便于审计日志记录危险操作。
/// </summary>
public sealed class CacheKeyDeleteResultDto
{
    /// <summary>是否删除成功。</summary>
    public bool Deleted { get; set; }

    /// <summary>被删除的 key。</summary>
    public string Key { get; set; } = string.Empty;
}
