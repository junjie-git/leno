using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 系统配置管理应用服务接口。
/// </summary>
public interface ISystemConfigAppService
{
    /// <summary>创建系统配置。</summary>
    Task<SystemConfigDto> CreateAsync(SaveSystemConfigDto dto, CancellationToken ct = default);

    /// <summary>更新系统配置（键不可变）。</summary>
    Task<SystemConfigDto> UpdateAsync(Guid configId, UpdateSystemConfigDto dto, CancellationToken ct = default);

    /// <summary>启用配置。</summary>
    Task EnableAsync(Guid configId, CancellationToken ct = default);

    /// <summary>停用配置。</summary>
    Task DisableAsync(Guid configId, CancellationToken ct = default);

    /// <summary>按键获取配置（加密配置值将被掩码）。</summary>
    Task<SystemConfigDto?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>按分组查询配置列表。</summary>
    Task<List<SystemConfigDto>> GetByGroupAsync(string group, CancellationToken ct = default);

    /// <summary>分页查询配置，支持键、分组、状态过滤。</summary>
    Task<SystemConfigListResultDto> QueryAsync(string? key, string? group, ConfigStatus? status, int page, int pageSize, CancellationToken ct = default);
}
