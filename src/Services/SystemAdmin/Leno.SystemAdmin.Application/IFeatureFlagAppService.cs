using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 特性开关管理应用服务接口。
/// </summary>
public interface IFeatureFlagAppService
{
    /// <summary>创建特性开关。</summary>
    Task<FeatureFlagDto> CreateAsync(SaveFeatureFlagDto dto, CancellationToken ct = default);

    /// <summary>更新特性开关（键不可变）。</summary>
    Task<FeatureFlagDto> UpdateAsync(Guid flagId, UpdateFeatureFlagDto dto, CancellationToken ct = default);

    /// <summary>启用开关。</summary>
    Task EnableAsync(Guid flagId, CancellationToken ct = default);

    /// <summary>停用开关。</summary>
    Task DisableAsync(Guid flagId, CancellationToken ct = default);

    /// <summary>按键获取开关。</summary>
    Task<FeatureFlagDto?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>分页查询开关，支持键与状态过滤。</summary>
    Task<FeatureFlagListResultDto> QueryAsync(string? key, FeatureFlagStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>按上下文评估开关是否生效。</summary>
    Task<bool> EvaluateAsync(EvaluateFlagDto dto, CancellationToken ct = default);
}
