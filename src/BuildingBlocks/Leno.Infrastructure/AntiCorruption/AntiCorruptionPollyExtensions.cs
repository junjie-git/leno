using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// 防腐层 Polly 策略扩展（M4.1 HTTP + P1-B.1 gRPC）。
/// HTTP 统一注入：重试 3 次（指数退避 1s/2s/4s）+ 熔断（失败率 50% 断 30s）+ Timeout 10s。
/// 网络故障（HttpRequestException/TaskCanceledException）触发重试与熔断计数。
/// gRPC 注入：仅对临时性故障（Unavailable/DeadlineExceeded/Aborted/ResourceExhausted）重试 2 次。
/// </summary>
public static class AntiCorruptionPollyExtensions
{
    public const string SectionName = "AntiCorruption:Polly";

    /// <summary>
    /// gRPC retry 策略的 keyed service 标识。
    /// 与 <see cref="GrpcAntiCorruptionClientBase.GrpcRetryPolicyKey"/> 保持一致。
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
    /// 注册 gRPC 防腐层 Polly retry 策略（P1-B.1）。
    /// 仅对 gRPC 临时性故障（Unavailable/DeadlineExceeded/Aborted/ResourceExhausted）重试，
    /// 业务错误（InvalidArgument/NotFound/PermissionDenied/Unauthenticated）不重试。
    /// 重试次数默认 2 次，指数退避 1s/2s，可由 <c>AntiCorruption:Polly:GrpcRetryCount</c> 配置覆盖。
    /// 与既有 HTTP <see cref="AddLenoAntiCorruptionPolly"/> 与 <c>CircuitBreakerState</c> 互不干扰：
    /// 本策略仅在 <see cref="GrpcAntiCorruptionClientBase.ExecuteAsync{T}"/> 内生效，
    /// <c>CircuitBreakerState</c> 在 <c>AntiCorruptionDispatcher</c> 外层独立判断。
    /// </summary>
    public static IServiceCollection AddLenoGrpcAntiCorruptionPolly(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var grpcRetryCount = section?.GetValue("GrpcRetryCount", 2) ?? 2;

        IAsyncPolicy grpcRetryPolicy = Policy
            .Handle<RpcException>(ex => IsTransientGrpcStatus(ex.StatusCode))
            .WaitAndRetryAsync(
                grpcRetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));

        services.AddKeyedSingleton<IAsyncPolicy>(GrpcRetryPolicyKey, grpcRetryPolicy);
        return services;
    }

    /// <summary>
    /// 判断 gRPC <see cref="StatusCode"/> 是否属于临时性故障（可安全重试）。
    /// 临时性故障：Unavailable（下游不可用）/ DeadlineExceeded（超时）/ Aborted（事务冲突）/ ResourceExhausted（限流）。
    /// 业务错误（InvalidArgument/NotFound/PermissionDenied/Unauthenticated 等）不在此列，重试无意义。
    /// </summary>
    internal static bool IsTransientGrpcStatus(StatusCode statusCode)
        => statusCode is StatusCode.Unavailable
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
