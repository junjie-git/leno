using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Leno.Infrastructure.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Inventory.Api.GrpcServices;

/// <summary>
/// gRPC 内部服务鉴权拦截器 —— 与 HTTP 内部路由的 <c>InternalApiKeyMiddleware</c> 对齐同一语义与同一密钥源
/// （<see cref="InternalApiKeyOptions.ApiKey"/>，配置键 <c>InternalAuth:ApiKey</c>）。
/// <para>
/// 双轨下线（2026-09-23）：库存 gRPC 面（<c>InventoryInternalService</c>）此前无任何鉴权 ——
/// 本拦截器使其与 internal/v1 HTTP 面同级防护：请求头 <c>x-internal-key</c> 固定时间比较；
/// 未配置密钥时非开发环境 fail-closed（PermissionDenied）。
/// </para>
/// </summary>
public sealed class InternalApiKeyServerInterceptor : Interceptor
{
    /// <summary>内部鉴权请求头（gRPC metadata 键为小写）。</summary>
    public const string HeaderName = "x-internal-key";

    private readonly InternalApiKeyOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<InternalApiKeyServerInterceptor> _logger;

    public InternalApiKeyServerInterceptor(
        IOptions<InternalApiKeyOptions> options,
        IHostEnvironment environment,
        ILogger<InternalApiKeyServerInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(context);

        var expected = _options.ApiKey;
        if (string.IsNullOrEmpty(expected))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning("内部鉴权密钥未配置，开发环境跳过 gRPC 校验 Method={Method}", context.Method);
                return await continuation(request, context).ConfigureAwait(false);
            }

            _logger.LogCritical("生产环境未配置 InternalAuth:ApiKey，拒绝 gRPC 调用 Method={Method}", context.Method);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "内部服务鉴权未配置"));
        }

        var provided = context.RequestHeaders.FirstOrDefault(h => h.Key == HeaderName)?.Value;
        if (string.IsNullOrEmpty(provided) || !FixedTimeEquals(provided, expected))
        {
            _logger.LogWarning("gRPC 内部鉴权失败 Method={Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "内部鉴权失败"));
        }

        return await continuation(request, context).ConfigureAwait(false);
    }

    private static bool FixedTimeEquals(string provided, string expected)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
