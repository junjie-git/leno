using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 通知调度器接口，供各事件消费者复用：查模板→查偏好→渲染→选渠道→创建记录→发送。
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// 调度通知发送。
    /// </summary>
    /// <param name="userId">接收用户标识。</param>
    /// <param name="templateCode">模板编码（如 OrderCreated）。</param>
    /// <param name="eventId">事件标识（用于幂等去重），可空。</param>
    /// <param name="variables">模板变量键值对。</param>
    Task DispatchAsync(Guid userId, string templateCode, Guid? eventId, Dictionary<string, string> variables, CancellationToken ct = default);
}
