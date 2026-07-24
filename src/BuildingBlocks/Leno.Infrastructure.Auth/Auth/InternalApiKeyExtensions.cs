using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// 内部服务间鉴权的启动校验扩展。
/// </summary>
public static class InternalApiKeyExtensions
{
    /// <summary>
    /// 启动时校验 <c>InternalAuth:ApiKey</c> 已配置。
    /// <para>
    /// 各 BC 的 <c>Program.cs</c> 在 <c>var app = builder.Build();</c> 之后、<c>app.Run()</c> 之前调用：
    /// <code>app.EnsureInternalApiKeyConfigured();</code>
    /// </para>
    /// Development 环境放行；生产/Staging 等非开发环境若 ApiKey 为空则抛出
    /// <see cref="InvalidOperationException"/> 阻止启动，避免忘配置导致内部端点完全开放。
    /// </summary>
    public static IApplicationBuilder EnsureInternalApiKeyConfigured(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment())
        {
            return app;
        }

        var options = app.ApplicationServices.GetRequiredService<IOptions<InternalApiKeyOptions>>().Value;
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new InvalidOperationException(
                "生产环境未配置 InternalAuth:ApiKey，拒绝启动。请在配置（InternalAuth:ApiKey）中设置非空内部鉴权密钥。");
        }

        return app;
    }
}
