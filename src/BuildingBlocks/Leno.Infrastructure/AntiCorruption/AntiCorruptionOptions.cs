namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层配置（M4.3）。
/// 通过 <c>AntiCorruption</c> 配置节绑定。
/// </summary>
public sealed class AntiCorruptionOptions
{
    /// <summary>是否启用 gRPC 模式（默认 false，灰度切换）。</summary>
    public bool UseGrpc { get; init; } = false;

    /// <summary>gRPC 服务端点地址映射（按 BC 名），如 <c>Order</c> -> <c>https://leno-order-api:5254</c>。</summary>
    public Dictionary<string, string> GrpcEndpoints { get; init; } = new();

    /// <summary>Polly 策略配置（M4.1）。</summary>
    public PollyOptions Polly { get; init; } = new();

    /// <summary>
    /// 防腐层调用方配置目标 BC 的 InternalApiKey（M5.2）。
    /// 键为目标 BC 名（如 <c>Product</c>），值用于注入 <c>X-Internal-Key</c> 请求头。
    /// 实际值通过 Consul KV 注入（<c>leno/security/internal-key/{bc}</c>），appsettings 仅保留占位符。
    /// </summary>
    public Dictionary<string, string> TargetInternalApiKeys { get; init; } = new();

    /// <summary>熔断器配置（M4 双轨方案）。null 时使用默认值 3/2/30s。</summary>
    public CircuitBreakerOptions? CircuitBreaker { get; init; }

    /// <summary>当前 BC 服务名（如 <c>order</c>），供 GrpcInternalKeyInterceptor 校验 internal key 时使用。</summary>
    public string? ServiceName { get; init; }

    /// <summary>当前 BC 接收 gRPC 调用时校验的 InternalApiKey（被调用方视角）。</summary>
    public string? InternalApiKey { get; init; }
}

/// <summary>
/// Polly 策略配置（M4.1）。
/// 通过 <c>AntiCorruption:Polly</c> 配置节绑定。
/// </summary>
public sealed class PollyOptions
{
    public int RetryCount { get; init; } = 3;
    public int CircuitBreakerDurationSeconds { get; init; } = 30;
    public int TimeoutSeconds { get; init; } = 10;
}

/// <summary>
/// 熔断器配置（M4 双轨方案）。
/// 通过 <c>AntiCorruption:CircuitBreaker</c> 配置节绑定。
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>连续失败次数阈值，达到后熔断 Open。默认 3。</summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>HalfOpen 状态下连续成功次数阈值，达到后熔断 Closed。默认 2。</summary>
    public int SuccessThreshold { get; init; } = 2;

    /// <summary>Open 状态持续时间（秒），过期后转 HalfOpen。默认 30。</summary>
    public int OpenDurationSeconds { get; init; } = 30;
}
