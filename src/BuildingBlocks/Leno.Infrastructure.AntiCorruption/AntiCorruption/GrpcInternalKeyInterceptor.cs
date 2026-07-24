using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// gRPC 服务端鉴权拦截器（M4 双轨方案）。
/// 校验 metadata header <c>x-internal-key</c>，与 HttpClient 模式 <c>X-Internal-Key</c> 语义一致。
/// 校验失败抛 <see cref="StatusCode.Unauthenticated"/>，调用方收到后由 Dispatcher 判定为业务异常不降级。
/// </summary>
public sealed class GrpcInternalKeyInterceptor : Interceptor
{
    private const string HeaderName = "x-internal-key";
    private readonly IOptionsMonitor<AntiCorruptionOptions> _options;
    private readonly ILogger<GrpcInternalKeyInterceptor> _logger;

    public GrpcInternalKeyInterceptor(
        IOptionsMonitor<AntiCorruptionOptions> options,
        ILogger<GrpcInternalKeyInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        var expectedKey = _options.CurrentValue.InternalApiKey;
        if (string.IsNullOrEmpty(expectedKey))
        {
            _logger.LogError("AntiCorruption:InternalApiKey 配置缺失，拒绝所有 gRPC 调用");
            throw new RpcException(new Status(StatusCode.Unauthenticated,
                "Internal API key not configured on server"));
        }

        var providedKey = context.RequestHeaders
            .FirstOrDefault(h => h.Key.Equals(HeaderName, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (string.IsNullOrEmpty(providedKey) || providedKey != expectedKey)
        {
            _logger.LogWarning("gRPC call rejected: invalid or missing x-internal-key header");
            throw new RpcException(new Status(StatusCode.Unauthenticated,
                "Invalid or missing x-internal-key"));
        }

        return await continuation(request, context).ConfigureAwait(false);
    }
}
