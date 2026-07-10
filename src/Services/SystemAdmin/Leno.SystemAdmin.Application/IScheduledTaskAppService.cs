using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 定时任务管理应用服务接口。
/// </summary>
public interface IScheduledTaskAppService
{
    /// <summary>创建定时任务（初始为停用态）。</summary>
    Task<ScheduledTaskDto> CreateAsync(SaveScheduledTaskDto dto, CancellationToken ct = default);

    /// <summary>更新定时任务（作业类型不可变）。</summary>
    Task<ScheduledTaskDto> UpdateAsync(Guid taskId, UpdateScheduledTaskDto dto, CancellationToken ct = default);

    /// <summary>启用任务并向调度器注册。</summary>
    Task EnableAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>停用任务并从调度器注销。</summary>
    Task DisableAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>立即触发任务执行。</summary>
    Task RunNowAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>按标识获取定时任务。</summary>
    Task<ScheduledTaskDto?> GetByIdAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>分页查询定时任务，支持名称与状态过滤。</summary>
    Task<ScheduledTaskListResultDto> QueryAsync(string? name, ScheduledTaskStatus? status, int page, int pageSize, CancellationToken ct = default);
}
