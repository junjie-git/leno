namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 请求处理委托接口（阶段四 4.2：可插拔策略链）。
/// <para>
/// 由 <see cref="GrpcAclChannel"/> 与 <see cref="HttpAclChannel"/> 等通道持有，
/// 将统一的 <see cref="AclRequest"/> 转换为具体协议（gRPC/HTTP）的远程调用，
/// 并将返回值包装为 <see cref="AclResponse"/>。
/// </para>
/// <para>
/// 通常由各 BC 在 DI 注册时传入 lambda：
/// <code>
/// services.AddSingleton&lt;IAclChannel&gt;(sp =&gt;
///     new GrpcAclChannel("product",
///         new DelegatingAclRequestHandler((req, ct) =&gt; InvokeGrpcProduct(req, ct))));
/// </code>
/// </para>
/// </summary>
public interface IAclRequestHandler
{
    /// <summary>处理 ACL 请求并返回响应。业务层失败返回 Success=false 的 AclResponse；基础设施层失败抛 AclChannelException。</summary>
    Task<AclResponse> HandleAsync(AclRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于委托的 <see cref="IAclRequestHandler"/> 实现，便于在 DI 注册时通过 lambda 表达。
/// </summary>
public sealed class DelegatingAclRequestHandler : IAclRequestHandler
{
    private readonly Func<AclRequest, CancellationToken, Task<AclResponse>> _handler;

    public DelegatingAclRequestHandler(Func<AclRequest, CancellationToken, Task<AclResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    public Task<AclResponse> HandleAsync(AclRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _handler(request, cancellationToken);
    }
}
