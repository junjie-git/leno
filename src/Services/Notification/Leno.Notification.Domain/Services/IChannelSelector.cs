using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 渠道选择器领域服务接口，负责根据渠道类型选择主备适配器。
/// 规则：
/// - Email 渠道默认 SMTP，无备选
/// - SMS 渠道根据配置的 Provider 字段选择 Aliyun 或 Tencent，另一家作为备选
/// - 仅可重试错误才触发同渠道内 failover，不可跨渠道 failover
/// </summary>
public interface IChannelSelector
{
    /// <summary>
    /// 选择指定渠道的主适配器提供商标识。
    /// </summary>
    /// <param name="channel">通知渠道。</param>
    /// <returns>提供商标识（如 "SMTP"、"Aliyun"、"Tencent"、"InApp"）。</returns>
    string SelectProvider(NotificationChannel channel);

    /// <summary>
    /// 选择当前配置的短信提供商名称（如 "Aliyun"、"Tencent"）。
    /// 供 <c>SmsChannel</c> 外壳类在运行时选择具体的 <see cref="ISmsProvider"/> 实现。
    /// </summary>
    /// <returns>短信提供商标识。</returns>
    string SelectSmsProvider();

    /// <summary>
    /// 选择指定渠道的备选适配器提供商标识。
    /// 无备选时返回 null。
    /// </summary>
    /// <param name="channel">通知渠道。</param>
    /// <returns>备选提供商标识，无备选则返回 null。</returns>
    string? SelectFallbackProvider(NotificationChannel channel);

    /// <summary>
    /// 判断给定错误码是否可重试，可重试的错误才允许 failover。
    /// 不可重试错误（如配置缺失、邮箱不存在）不触发 failover。
    /// </summary>
    /// <param name="errorCode">渠道返回的错误码。</param>
    /// <returns>true 表示可重试，允许 failover。</returns>
    bool IsRetryableError(string? errorCode);

    /// <summary>
    /// 判断在给定错误下是否应触发 failover。
    /// 仅可重试错误且目标渠道存在备选适配器时才返回 true。
    /// 不可跨渠道 failover（Email 不会 failover 到 SMS）。
    /// </summary>
    /// <param name="channel">当前渠道。</param>
    /// <param name="errorCode">错误码。</param>
    /// <returns>true 表示应触发 failover。</returns>
    bool ShouldFailover(NotificationChannel channel, string? errorCode);
}