namespace Leno.Payment.Domain.Services;

/// <summary>
/// 异步通知模式枚举，描述渠道通知到达的方式。
/// </summary>
public enum AsyncNotifyMode
{
    /// <summary>无异步通知。</summary>
    None = 0,

    /// <summary>HTTP 回调（被动接收渠道推送）。</summary>
    HttpCallback = 1,

    /// <summary>主动轮询（应用拉取渠道状态）。</summary>
    Polling = 2,

    /// <summary>HTTP 回调 + 主动轮询（双轨，互为兜底）。</summary>
    Both = 3
}

/// <summary>
/// 支付渠道能力声明，描述渠道支持的功能特性。
/// 用于驱动退款/查询/通知处理等能力的条件分支，避免在调用方硬编码渠道判断。
/// </summary>
public sealed class PaymentChannelCapabilities
{
    /// <summary>是否支持退款。</summary>
    public bool SupportsRefund { get; init; }

    /// <summary>是否支持部分捕获（部分扣款/部分支付）。</summary>
    public bool SupportsPartialCapture { get; init; }

    /// <summary>是否支持主动查询支付/退款状态。</summary>
    public bool SupportsQuery { get; init; }

    /// <summary>异步通知模式。</summary>
    public AsyncNotifyMode AsyncNotifyMode { get; init; }

    /// <summary>默认能力集：支持退款 + 支持查询 + HTTP 回调与轮询双轨。</summary>
    public static PaymentChannelCapabilities Default { get; } = new()
    {
        SupportsRefund = true,
        SupportsPartialCapture = false,
        SupportsQuery = true,
        AsyncNotifyMode = AsyncNotifyMode.Both
    };

    /// <summary>仅支持退款（不支持查询/通知）的最小能力集。</summary>
    public static PaymentChannelCapabilities RefundOnly { get; } = new()
    {
        SupportsRefund = true,
        SupportsPartialCapture = false,
        SupportsQuery = false,
        AsyncNotifyMode = AsyncNotifyMode.None
    };
}
