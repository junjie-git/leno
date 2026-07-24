using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// HTTP ACL 通道（阶段四 4.2：可插拔策略链）。
/// <para>
/// 优先级 1（次高，作为 gRPC 的降级备份）。
/// 通过 <see cref="HttpClient"/> 发送 JSON 请求到目标服务内部 API，
/// 将 <see cref="AclRequest"/> 转换为 <c>POST {TargetService}/internal/{OperationName}</c>。
/// </para>
/// <para>
/// 与 <see cref="GrpcAclChannel"/> 形成策略链：gRPC 优先失败降级到 HTTP。
/// 双轨期支持 feature flag 按 BC 切流（旧 UseGrpc=false 时仅 HTTP 通道激活）。
/// </para>
/// </summary>
public class HttpAclChannel : AclChannelBase
{
    /// <summary>HTTP 通道默认优先级（1，次高，作为 gRPC 降级备份）。</summary>
    public const int DefaultPriority = 1;

    private readonly HttpClient _httpClient;
    private readonly Func<AclRequest, Uri> _requestUriBuilder;
    private readonly Func<AclRequest, string>? _requestBodyBuilder;
    private readonly Func<HttpResponseMessage, AclRequest, CancellationToken, Task<AclResponse>> _responseParser;

    /// <summary>HTTP 通道名（"http"）。</summary>
    public override string Name => "http";

    protected override string ServiceName { get; }

    /// <summary>
    /// 构造 HTTP ACL 通道。
    /// </summary>
    /// <param name="serviceName">防腐层服务标识（如 "product"）。</param>
    /// <param name="httpClient">已配置 BaseAddress 与 Polly 策略的 HttpClient 实例。</param>
    /// <param name="requestUriBuilder">构造请求 URI 的委托；默认基于 BaseAddress + <c>internal/{OperationName}</c>。</param>
    /// <param name="requestBodyBuilder">构造请求 Body 的委托；为 null 时使用 <see cref="AclRequest.Payload"/> 的 JSON 序列化。</param>
    /// <param name="responseParser">解析响应的委托；默认将响应 JSON 反序列化为 AclResponse。</param>
    /// <param name="priority">优先级，默认 1。</param>
    /// <param name="logger">日志记录器。</param>
    public HttpAclChannel(
        string serviceName,
        HttpClient httpClient,
        Func<AclRequest, Uri>? requestUriBuilder = null,
        Func<AclRequest, string>? requestBodyBuilder = null,
        Func<HttpResponseMessage, AclRequest, CancellationToken, Task<AclResponse>>? responseParser = null,
        int priority = DefaultPriority,
        ILogger<HttpAclChannel>? logger = null)
        : base(priority, supportsSynchronous: true, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(httpClient);
        ServiceName = serviceName;
        _httpClient = httpClient;
        _requestUriBuilder = requestUriBuilder ?? BuildDefaultUri;
        _requestBodyBuilder = requestBodyBuilder;
        _responseParser = responseParser ?? ParseDefaultResponse;
    }

    protected override async Task<AclResponse> SendCoreAsync(AclRequest request, CancellationToken cancellationToken)
    {
        var uri = _requestUriBuilder(request);
        var body = _requestBodyBuilder is not null
            ? _requestBodyBuilder(request)
            : SerializePayload(request.Payload);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        // TraceId 通过 X-Trace-Id 头传播，便于跨服务调用链关联
        if (request.TraceId != Guid.Empty)
        {
            httpRequest.Headers.TryAddWithoutValidation("X-Trace-Id", request.TraceId.ToString("D"));
        }

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        return await _responseParser(response, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 默认 URI 构造：{BaseAddress}/internal/{OperationName}。
    /// </summary>
    private static Uri BuildDefaultUri(AclRequest request)
    {
        var baseUri = request.TargetService;
        var relative = $"internal/{request.OperationName}";
        return new Uri($"{baseUri.TrimEnd('/')}/{relative}", UriKind.RelativeOrAbsolute);
    }

    /// <summary>
    /// 默认响应解析：2xx → 反序列化为 AclResponse；非 2xx → AclResponse.Fail。
    /// 网络故障由 <see cref="AclChannelBase.SendAsync"/> 捕获 HttpRequestException 包装为 AclChannelException。
    /// </summary>
    private static async Task<AclResponse> ParseDefaultResponse(
        HttpResponseMessage response,
        AclRequest request,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorCode = $"{request.TargetService.ToUpperInvariant()}_REMOTE_FAILED";
            var errorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
            // 尝试从响应 body 解析 ErrorCode（业务错误格式：{"errorCode": "...", "errorMessage": "..."}）
            var body = DeserializeBody(responseBody);
            if (body is not null)
            {
                if (body.TryGetValue("errorCode", out var code) && code is string codeStr)
                    errorCode = codeStr;
                if (body.TryGetValue("errorMessage", out var msg) && msg is string msgStr)
                    errorMessage = msgStr;
            }
            return AclResponse.Fail(errorCode, errorMessage);
        }

        var bodyDict = DeserializeBody(responseBody);
        return AclResponse.Ok(bodyDict);
    }

    /// <summary>
    /// 健康检查：通过 GET {BaseAddress}/health/live 探测服务可用性。
    /// 返回 2xx 认为健康，其他状态码或异常认为不健康。
    /// </summary>
    public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "health/live");
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "HTTP ACL channel {Service} health check failed", ServiceName);
            return false;
        }
    }
}
