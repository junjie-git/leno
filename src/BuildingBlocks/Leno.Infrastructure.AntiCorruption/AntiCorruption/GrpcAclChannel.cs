using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// gRPC ACL 通道（阶段四 4.2：可插拔策略链）。
/// <para>
/// 优先级 0（最高，作为主通道）。封装 <see cref="IAclRequestHandler"/> 委托，
/// 将 <see cref="AclRequest"/> 转换为 gRPC 调用并返回 <see cref="AclResponse"/>。
/// </para>
/// <para>
/// 与 <see cref="HttpAclChannel"/> 形成策略链：gRPC 优先，失败降级到 HTTP。
/// 新协议（消息总线等）通过实现 <see cref="IAclChannel"/> 并指定更高 Priority 接入。
/// </para>
/// <para>
/// 派生类可重写 <see cref="SendCoreAsync"/> 实现特定 gRPC 服务调用逻辑（如 ProductInternalService），
/// 或通过构造函数注入 <see cref="IAclRequestHandler"/> 委托实现零侵入接入。
/// </para>
/// </summary>
public class GrpcAclChannel : AclChannelBase
{
    /// <summary>gRPC 通道默认优先级（最高，0）。</summary>
    public const int DefaultPriority = 0;

    private readonly IAclRequestHandler? _handler;

    /// <summary>gRPC 通道名（"grpc"）。</summary>
    public override string Name => "grpc";

    protected override string ServiceName { get; }

    /// <summary>
    /// 构造 gRPC ACL 通道。
    /// </summary>
    /// <param name="serviceName">防腐层服务标识（如 "product"），用于指标埋点。</param>
    /// <param name="handler">请求处理委托；为 null 时派生类必须重写 <see cref="SendCoreAsync"/>。</param>
    /// <param name="priority">优先级，默认 0（最高）。</param>
    /// <param name="logger">日志记录器。</param>
    public GrpcAclChannel(
        string serviceName,
        IAclRequestHandler? handler = null,
        int priority = DefaultPriority,
        ILogger<GrpcAclChannel>? logger = null)
        : base(priority, supportsSynchronous: true, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ServiceName = serviceName;
        _handler = handler;
    }

    protected override Task<AclResponse> SendCoreAsync(AclRequest request, CancellationToken cancellationToken)
    {
        if (_handler is null)
        {
            throw new AclChannelException(Name,
                $"GrpcAclChannel 未配置 handler 且派生类未重写 {nameof(SendCoreAsync)}，无法处理 {request.TargetService}/{request.OperationName}");
        }
        return _handler.HandleAsync(request, cancellationToken);
    }

    /// <summary>
    /// 默认健康检查实现：gRPC 通道默认假设健康，由真实调用失败触发熔断评估。
    /// 派生类可重写为 gRPC HealthCheck 协议探测。
    /// </summary>
    public override Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

/// <summary>
/// gRPC ACL 通道泛型版本：绑定到具体 gRPC 客户端类型，便于派生类封装特定服务调用。
/// </summary>
public class GrpcAclChannel<TClient> : GrpcAclChannel
    where TClient : class
{
    private readonly TClient _client;
    private readonly Func<TClient, AclRequest, CancellationToken, Task<AclResponse>> _invoker;

    /// <summary>绑定的 gRPC 客户端实例。</summary>
    public TClient Client => _client;

    /// <summary>
    /// 构造绑定到具体 gRPC 客户端的 GrpcAclChannel。
    /// </summary>
    /// <param name="serviceName">防腐层服务标识。</param>
    /// <param name="client">gRPC 客户端实例（如 ProductInternalServiceClient）。</param>
    /// <param name="invoker">调用委托：将 AclRequest 转换为 gRPC 调用并返回 AclResponse。</param>
    /// <param name="priority">优先级，默认 0。</param>
    /// <param name="logger">日志记录器。</param>
    public GrpcAclChannel(
        string serviceName,
        TClient client,
        Func<TClient, AclRequest, CancellationToken, Task<AclResponse>> invoker,
        int priority = DefaultPriority,
        ILogger<GrpcAclChannel<TClient>>? logger = null)
        : base(serviceName, handler: null, priority: priority, logger: logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(invoker);
        _client = client;
        _invoker = invoker;
    }

    protected override async Task<AclResponse> SendCoreAsync(AclRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _invoker(_client, request, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            throw new AclChannelException(Name,
                $"gRPC 调用 {request.TargetService}/{request.OperationName} 失败：StatusCode={ex.StatusCode} Detail={ex.Status.Detail}",
                ex);
        }
    }
}
