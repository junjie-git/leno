using Leno.SystemAdmin.Application.DTOs;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 限流规则管理应用服务接口。
/// </summary>
public interface IRateLimitRuleAppService
{
    /// <summary>分页查询限流规则，支持 API 路径与启用状态过滤。</summary>
    Task<RateLimitRuleListResultDto> QueryAsync(string? targetApi, bool? enabled, int page, int pageSize, CancellationToken ct = default);

    /// <summary>按标识获取限流规则详情。</summary>
    Task<RateLimitRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>创建限流规则。</summary>
    Task<RateLimitRuleDto> CreateAsync(SaveRateLimitRuleDto dto, CancellationToken ct = default);

    /// <summary>更新限流规则，返回 409 当乐观并发冲突。</summary>
    Task<RateLimitRuleDto> UpdateAsync(Guid id, SaveRateLimitRuleDto dto, CancellationToken ct = default);

    /// <summary>启用限流规则。</summary>
    Task EnableAsync(Guid id, CancellationToken ct = default);

    /// <summary>停用限流规则。</summary>
    Task DisableAsync(Guid id, CancellationToken ct = default);
}