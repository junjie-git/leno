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
