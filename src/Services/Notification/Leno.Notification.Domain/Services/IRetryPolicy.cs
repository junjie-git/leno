namespace Leno.Notification.Domain.Services;

/// <summary>
/// 重试策略领域服务接口，定义错误分类与退避策略。
/// 由基础设施层实现，供重试任务使用。
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// 判断给定错误码是否可重试。
    /// </summary>
    /// <param name="errorCode">发送渠道返回的错误码。</param>
    /// <returns>true 表示可重试，false 表示不可重试应直接进入死信。</returns>
    bool ShouldRetry(string? errorCode);

    /// <summary>
    /// 根据已重试次数计算下一次重试延迟。
    /// 采用指数退避策略：30s / 2min / 10min。
    /// </summary>
    /// <param name="retryCount">当前已重试次数（1-based）。</param>
    /// <returns>下一次重试的延迟时间。</returns>
    TimeSpan NextDelay(int retryCount);
}