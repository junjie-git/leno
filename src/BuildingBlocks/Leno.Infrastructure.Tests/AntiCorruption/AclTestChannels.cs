using Leno.Infrastructure.AntiCorruption;

namespace Leno.Infrastructure.Tests.AntiCorruption;

/// <summary>
/// 测试用 IAclChannel 实现：可配置 Name/Priority/HealthCheck 返回值、Send 行为。
/// 用于策略链遍历、熔断降级、优先级选择等场景的单元测试。
/// </summary>
internal sealed class TestAclChannel : IAclChannel
{
    public string Name { get; }
    public int Priority { get; }
    public bool SupportsSynchronous { get; }

    /// <summary>HealthCheckAsync 返回值；默认 true。可设为 false 模拟通道不可用。</summary>
    public Func<CancellationToken, Task<bool>> HealthCheckImpl { get; set; } = _ => Task.FromResult(true);

    /// <summary>SendAsync 实现；默认返回成功响应。可设为抛 AclChannelException 模拟基础设施失败。</summary>
    public Func<AclRequest, CancellationToken, Task<AclResponse>> SendImpl { get; set; } = (_, _) => Task.FromResult(AclResponse.EmptyOk());

    /// <summary>记录 SendAsync 被调用次数，用于断言策略链遍历命中。</summary>
    public int SendCallCount { get; private set; }

    /// <summary>记录 HealthCheckAsync 被调用次数。</summary>
    public int HealthCheckCallCount { get; private set; }

    public TestAclChannel(string name, int priority, bool supportsSynchronous = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Priority = priority;
        SupportsSynchronous = supportsSynchronous;
    }

    public Task<AclResponse> SendAsync(AclRequest request, CancellationToken cancellationToken = default)
    {
        SendCallCount++;
        return SendImpl(request, cancellationToken);
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        HealthCheckCallCount++;
        return HealthCheckImpl(cancellationToken);
    }
}

/// <summary>
/// 测试用 IAclRequestHandler 实现：可配置 HandleImpl 行为。
/// </summary>
internal sealed class TestAclRequestHandler : IAclRequestHandler
{
    public Func<AclRequest, CancellationToken, Task<AclResponse>> HandleImpl { get; set; } = (_, _) => Task.FromResult(AclResponse.EmptyOk());
    public int HandleCallCount { get; private set; }

    public Task<AclResponse> HandleAsync(AclRequest request, CancellationToken cancellationToken = default)
    {
        HandleCallCount++;
        return HandleImpl(request, cancellationToken);
    }
}
