namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 通道异常（阶段四 4.2：可插拔策略链）。
/// <para>
/// 由 <see cref="IAclChannel.SendAsync"/> 在基础设施层失败时抛出（网络故障、超时、协议解析失败等）。
/// 业务层失败应通过 <see cref="AclResponse"/> 的 <c>Success=false</c> 返回，不抛此异常。
/// </para>
/// <para>
/// 调度器 (<see cref="AntiCorruptionDispatcher"/>) 捕获此异常后：
/// <list type="bullet">
/// <item>记录熔断失败，触发熔断评估</item>
/// <item>尝试下一优先级通道</item>
/// <item>所有通道耗尽时抛 <see cref="ChannelName"/>=&quot;all&quot; 的 <see cref="AclChannelException"/></item>
/// </list>
/// </para>
/// </summary>
public sealed class AclChannelException : Exception
{
    /// <summary>失败通道名（如 "grpc"），全通道耗尽时为 "all"。</summary>
    public string ChannelName { get; }

    public AclChannelException(string channelName, string message, Exception? inner = null)
        : base(message, inner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        ChannelName = channelName;
    }
}
