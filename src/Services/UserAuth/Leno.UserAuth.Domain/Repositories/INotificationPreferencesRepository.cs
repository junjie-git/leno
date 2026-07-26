using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Aggregates;

namespace Leno.UserAuth.Domain.Repositories;

/// <summary>
/// 通知偏好仓储接口，定义在领域层，由基础设施层实现。
/// 同一用户仅一条 <see cref="NotificationPreferences"/> 聚合，懒初始化由应用层负责。
/// </summary>
public interface INotificationPreferencesRepository : IRepository<NotificationPreferences>
{
    /// <summary>按用户标识查询通知偏好，不存在返回 null。</summary>
    Task<NotificationPreferences?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
