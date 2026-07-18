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
