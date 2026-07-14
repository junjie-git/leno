using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.ApiGateway.Extensions;

/// <summary>
/// 超时服务注册扩展。
/// 使用 ASP.NET Core 8+ <c>AddRequestTimeouts</c> 注册命名超时策略，
/// 与 YARP 路由级 <c>TimeoutPolicy</c> 字段集成（YARP 2.1+ 支持）。
/// </summary>
public static class TimeoutExtensions
{
    /// <summary>
    /// 注册命名超时策略：default(30s) / seckill(5s) / upload(120s) / internal(15s)。
    /// 路由通过 <c>ReverseProxy:Routes[*].TimeoutPolicy</c> 引用对应策略名。
    /// </summary>
    public static IServiceCollection AddGatewayTimeouts(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TimeoutOptions>(configuration.GetSection("Timeout"));

        var opts = configuration.GetSection("Timeout").Get<TimeoutOptions>()
            ?? new TimeoutOptions();

        services.AddRequestTimeouts(options =>
        {
            foreach (var (policyName, policyOpts) in opts.Policies)
            {
                options.AddPolicy(policyName, policyOpts.RequestTimeout);
            }

            // 默认策略（无显式 TimeoutPolicy 的路由应用）。
            // 注意：RequestTimeoutPolicy 无 PolicyName 字段，策略名仅为 AddPolicy 的键。
            if (!string.IsNullOrEmpty(opts.DefaultPolicy)
                && opts.Policies.TryGetValue(opts.DefaultPolicy, out var defaultOpts))
            {
                options.DefaultPolicy = new RequestTimeoutPolicy
                {
                    Timeout = defaultOpts.RequestTimeout
                };
            }
        });

        return services;
    }
}
