namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 防腐层通道抽象（阶段四 4.2：可插拔策略链）。
/// <para>
/// 每个协议（gRPC/HTTP/消息总线）实现一个 channel，调度器
/// (<see cref="AntiCorruptionDispatcher"/>) 按优先级 + 熔断状态选择可用 channel。
/// 新协议接入零侵入：仅需实现本接口并注册到 DI 容器即可。
/// </para>
/// </summary>
public interface IAclChannel
{
    /// <summary>通道唯一标识（如 "grpc", "http", "message-bus"）。</summary>
    string Name { get; }

    /// <summary>优先级（数值越小优先级越高，0 = 最高）。</summary>
    int Priority { get; }

    /// <summary>是否支持同步请求-响应语义（消息总线异步通道返回 false）。</summary>
    bool SupportsSynchronous { get; }

    /// <summary>
    /// 发送请求并返回响应。
    /// 失败抛 <see cref="AclChannelException"/>；业务错误通过 <see cref="AclResponse"/> 的
    /// <c>Success=false</c> + <c>ErrorCode</c> 返回。
    /// </summary>
    Task<AclResponse> SendAsync(AclRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 健康检查：用于调度器选择前判断通道可用性。
    /// 返回 false 则调度器跳过该通道并触发熔断评估。
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
