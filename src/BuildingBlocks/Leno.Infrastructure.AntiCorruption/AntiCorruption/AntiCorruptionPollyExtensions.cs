using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层 HttpClient Polly 策略扩展（M4.1）。
/// 统一注入：重试 3 次（指数退避 1s/2s/4s）+ 熔断（失败率 50% 断 30s）+ Timeout 10s。
/// 网络故障（HttpRequestException/TaskCanceledException）触发重试与熔断计数。
/// </summary>
public static class AntiCorruptionPollyExtensions
{
    public const string SectionName = "AntiCorruption:Polly";

    /// <summary>
    /// gRPC 重试策略的 keyed service 标识，由 <see cref="AddLenoGrpcAntiCorruptionPolly"/> 注册。
    /// <see cref="GrpcAntiCorruptionClientBase"/> 通过此 key 解析 <see cref="IAsyncPolicy"/>。
    /// </summary>
    public const string GrpcRetryPolicyKey = "GrpcAntiCorruptionRetry";

    public static IServiceCollection AddLenoAntiCorruptionPolly(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var retryCount = section?.GetValue("RetryCount", 3) ?? 3;
        var circuitBreakerDurationSeconds = section?.GetValue("CircuitBreakerDurationSeconds", 30) ?? 30;
        var timeoutSeconds = section?.GetValue("TimeoutSeconds", 10) ?? 10;

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 10,
                durationOfBreak: TimeSpan.FromSeconds(circuitBreakerDurationSeconds));

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
            timeoutSeconds,
            TimeoutStrategy.Pessimistic);

        services.AddKeyedSingleton("AntiCorruptionRetry", retryPolicy);
        services.AddKeyedSingleton("AntiCorruptionCircuitBreaker", circuitBreakerPolicy);
        services.AddKeyedSingleton("AntiCorruptionTimeout", timeoutPolicy);

        return services;
    }

    /// <summary>
    /// 注册 gRPC 防腐层 Polly 重试策略（P1-B.1 问题 9）。
    /// <para>
    /// 在 <see cref="GrpcAntiCorruptionClientBase.ExecuteAsync{T}"/> 内嵌执行，对所有 gRPC 派生类自动生效。
    /// 仅对临时性 gRPC 故障重试：<see cref="StatusCode.Unavailable"/>、<see cref="StatusCode.DeadlineExceeded"/>、
    /// <see cref="StatusCode.Aborted"/>、<see cref="StatusCode.ResourceExhausted"/>。
    /// 业务错误（InvalidArgument/NotFound/PermissionDenied 等）与 <see cref="DomainException"/> 不重试。
    /// </para>
    /// <para>
    /// 重试次数保守（默认 2 次），指数退避（1s, 2s），避免放大下游压力。
    /// 与既有 <see cref="CircuitBreakerState"/> 共存：Polly 在单次调用内重试，
    /// <see cref="CircuitBreakerState"/> 在外层判断是否降级到 HttpClient，互不干扰。
    /// </para>
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置，读取 <c>AntiCorruption:Polly:GrpcRetryCount</c>（默认 2）。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLenoGrpcAntiCorruptionPolly(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        var section = configuration.GetSection(SectionName);
        var grpcRetryCount = section?.GetValue("GrpcRetryCount", 2) ?? 2;

        // gRPC retry：仅对临时性故障重试，业务错误与领域异常不重试
        var grpcRetryPolicy = Policy
            .Handle<RpcException>(ex => IsTransientGrpcStatus(ex.StatusCode))
            .WaitAndRetryAsync(grpcRetryCount, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));

        services.AddKeyedSingleton<IAsyncPolicy>(GrpcRetryPolicyKey, grpcRetryPolicy);
        return services;
    }

    /// <summary>
    /// 判断 gRPC <see cref="StatusCode"/> 是否属于临时性故障（可重试）。
    /// 业务错误（InvalidArgument/NotFound/PermissionDenied/Unauthenticated）不重试，
    /// 避免无意义重试放大下游压力。
    /// </summary>
    /// <param name="statusCode">gRPC 状态码。</param>
    /// <returns>是临时性故障返回 true，否则 false。</returns>
    public static bool IsTransientGrpcStatus(StatusCode statusCode) =>
        statusCode is StatusCode.Unavailable
            or StatusCode.DeadlineExceeded
            or StatusCode.Aborted
            or StatusCode.ResourceExhausted;

    public static IAsyncPolicy<HttpResponseMessage>[] GetAntiCorruptionPolicies(
        IServiceProvider services)
    {
        var retry = services.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionRetry");
        var circuit = services.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionCircuitBreaker");
        var timeout = services.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionTimeout");
        return [retry, circuit, timeout];
    }

    /// <summary>
    /// 链式追加防腐层 Polly 策略到 <see cref="IHttpClientBuilder"/>。
    /// 各 BC 在 <c>AddHttpClient&lt;TInterface, TImpl&gt;(...).AddAntiCorruptionPolicies()</c> 调用。
    /// </summary>
    public static IHttpClientBuilder AddAntiCorruptionPolicies(this IHttpClientBuilder builder)
    {
        builder.AddPolicyHandler((sp, _) =>
            sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionRetry"));
        builder.AddPolicyHandler((sp, _) =>
            sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionCircuitBreaker"));
        builder.AddPolicyHandler((sp, _) =>
            sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("AntiCorruptionTimeout"));
        return builder;
    }
}
