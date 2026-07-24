using System.Collections.ObjectModel;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 防腐层请求模型（阶段四 4.2：可插拔策略链）。
/// <para>
/// 表示一次跨 BC 调用：操作名（<see cref="OperationName"/>）、目标服务
/// （<see cref="TargetService"/>）、负载字典（<see cref="Payload"/>）与
/// 可选的 <see cref="TraceId"/>（用于跨通道链路追踪关联）。
/// </para>
/// <para>
/// 通道实现按自身协议将 <see cref="Payload"/> 转换为 gRPC 请求、HTTP JSON Body 等。
/// </para>
/// </summary>
public sealed record AclRequest
{
    /// <summary>操作名（如 "get_sku_info"），用于指标埋点与日志。</summary>
    public string OperationName { get; init; }

    /// <summary>目标服务名（如 "product"），用于指标埋点与路由。</summary>
    public string TargetService { get; init; }

    /// <summary>请求负载键值对（参数集合）。空字典表示无参数操作。</summary>
    public IReadOnlyDictionary<string, object> Payload { get; init; }

    /// <summary>跨通道链路追踪 ID；缺省时由调度器自动生成。</summary>
    public Guid TraceId { get; init; }

    public AclRequest(
        string operationName,
        string targetService,
        IReadOnlyDictionary<string, object>? payload = null,
        Guid traceId = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetService);
        OperationName = operationName;
        TargetService = targetService;
        Payload = payload ?? ReadOnlyDictionary<string, object>.Empty;
        TraceId = traceId;
    }
}
