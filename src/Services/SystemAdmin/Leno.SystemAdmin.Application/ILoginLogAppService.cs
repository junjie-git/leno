using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 登录日志查询应用服务接口（只读，日志由 LoginLogConsumer 异步写入）。
/// </summary>
public interface ILoginLogAppService
{
    /// <summary>分页查询登录日志。</summary>
    Task<LoginLogListResultDto> QueryAsync(LoginLogQuery query, CancellationToken ct = default);

    /// <summary>按标识获取登录日志详情，不存在返回 null。</summary>
    Task<LoginLogDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>导出登录日志为 CSV，单次最多 10 万条。</summary>
    Task<string> ExportAsync(LoginLogQuery query, CancellationToken ct = default);
}
