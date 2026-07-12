using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 通知统一发送入口，封装模板查找、渲染、渠道发送、状态更新全流程。
/// 供 API 控制器或事件消费者调用。
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 发送通知，返回发送结果。
    /// </summary>
    /// <param name="request">通知发送请求，包含模板编码、用户、变量等信息。</param>
    /// <param name="ct">取消令牌。</param>
    Task<NotificationSendResult> SendAsync(NotificationRequest request, CancellationToken ct = default);
}

/// <summary>
/// 通知发送结果。
/// </summary>
public class NotificationSendResult
{
    /// <summary>是否发送成功。</summary>
    public bool Succeeded { get; set; }

    /// <summary>通知记录标识。</summary>
    public Guid? RecordId { get; set; }

    /// <summary>错误码。</summary>
    public string? ErrorCode { get; set; }

    /// <summary>错误信息。</summary>
    public string? ErrorMessage { get; set; }
}