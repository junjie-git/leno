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
