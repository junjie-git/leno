using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Leno.Order.Infrastructure.AntiCorruption;

/// <summary>
/// gRPC 内部服务鉴权客户端拦截器 —— 为每个出站调用附加 <c>x-internal-key</c> 请求头，
/// 与服务端 <c>InternalApiKeyServerInterceptor</c> / HTTP 面 <c>InternalApiKeyMiddleware</c> 同一密钥源
/// （配置键 <c>InternalAuth:ApiKey</c>）。
/// </summary>
public sealed class InternalApiKeyClientInterceptor : Interceptor
{
    /// <summary>内部鉴权请求头。</summary>
    public const string HeaderName = "x-internal-key";

    private readonly string? _apiKey;

    public InternalApiKeyClientInterceptor(string? apiKey)
    {
        _apiKey = apiKey;
    }

    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        if (!string.IsNullOrEmpty(_apiKey))
        {
            var headers = context.Options.Headers ?? new Metadata();
            headers.Add(HeaderName, _apiKey);
            context = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method, context.Host, context.Options.WithHeaders(headers));
        }

        return continuation(request, context);
    }
}
