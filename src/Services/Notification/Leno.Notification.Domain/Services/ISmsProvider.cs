using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 短信发送提供商接口，由 Aliyun/Tencent 等具体实现。
/// 渠道防腐层接口，定义在领域层，由基础设施层实现。
/// </summary>
public interface ISmsProvider
{
    /// <summary>提供商名称（如 "Aliyun"、"Tencent"）。</summary>
    string ProviderName { get; }

    /// <summary>
    /// 发送短信，返回发送结果。
    /// </summary>
    /// <param name="request">渠道发送请求。</param>
    /// <param name="ct">取消令牌。</param>
    Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default);
}
