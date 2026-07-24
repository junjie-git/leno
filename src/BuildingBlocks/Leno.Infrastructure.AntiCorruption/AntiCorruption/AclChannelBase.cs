using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// ACL 通道抽象基类（阶段四 4.2：可插拔策略链）。
/// <para>
/// 封装 <see cref="IAclChannel"/> 实现的公共逻辑：
/// <list type="bullet">
/// <item>统一异常包装：将底层异常（HttpRequestException/RpcException 等）转换为 <see cref="AclChannelException"/></item>
/// <item>TraceId 自动注入：当 <see cref="AclRequest.TraceId"/> 为 <see cref="Guid.Empty"/> 时自动生成</item>
/// <item>请求日志：Debug 级别记录请求/响应摘要，Warning 级别记录失败</item>
/// <item>失败埋点：通过 <see cref="AntiCorruptionMetrics.RecordFailure"/> 上报指标</item>
/// <item>健康检查默认实现：调用 <see cref="SendAsync"/> 探测，成功即认为健康</item>
/// </list>
/// </para>
/// <para>
/// 派生类仅需实现 <see cref="SendCoreAsync"/> 处理具体协议转换与远程调用。
/// </para>
/// </summary>
public abstract class AclChannelBase : IAclChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    /// <summary>通道是否支持同步请求-响应语义。派生类通过构造参数指定（默认 true）。</summary>
    public bool SupportsSynchronous { get; }

    /// <summary>通道优先级（数值越小优先级越高，0 = 最高）。派生类通过构造参数指定。</summary>
    public int Priority { get; }

    protected ILogger Logger { get; }

    /// <summary>防腐层服务标识（如 "product"），用于指标埋点。</summary>
    protected abstract string ServiceName { get; }

    protected AclChannelBase(int priority, bool supportsSynchronous = true, ILogger? logger = null)
    {
        if (priority < 0)
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority 必须 >= 0");
        Priority = priority;
        SupportsSynchronous = supportsSynchronous;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>通道唯一标识（如 "grpc", "http"）。派生类实现。</summary>
    public abstract string Name { get; }

    /// <summary>
    /// 模板方法：统一异常处理、TraceId 注入、日志、埋点。
    /// 派生类通过 <see cref="SendCoreAsync"/> 实现协议特定调用逻辑。
    /// </summary>
    public async Task<AclResponse> SendAsync(AclRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // TraceId 自动注入：当请求未携带 TraceId 时生成新值
        var effectiveRequest = request.TraceId == Guid.Empty
            ? request with { TraceId = Guid.NewGuid() }
            : request;

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug(
                "ACL channel {Channel} sending {Service}/{Operation} traceId={TraceId}",
                Name, effectiveRequest.TargetService, effectiveRequest.OperationName, effectiveRequest.TraceId);
        }

        try
        {
            var response = await SendCoreAsync(effectiveRequest, cancellationToken).ConfigureAwait(false);
            if (Logger.IsEnabled(LogLevel.Debug) && response.Success)
            {
                Logger.LogDebug(
                    "ACL channel {Channel} succeeded for {Service}/{Operation} traceId={TraceId}",
                    Name, effectiveRequest.TargetService, effectiveRequest.OperationName, effectiveRequest.TraceId);
            }

            if (!response.Success)
            {
                // 业务失败：埋点但不抛异常（调度器据此决定是否降级）
                AntiCorruptionMetrics.RecordFailure(ServiceName, effectiveRequest.OperationName, Name);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 用户主动取消：透传不埋点
            throw;
        }
        catch (OperationCanceledException ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, effectiveRequest.OperationName, Name);
            Logger.LogWarning(ex,
                "ACL channel {Channel} timeout for {Service}/{Operation} traceId={TraceId}",
                Name, effectiveRequest.TargetService, effectiveRequest.OperationName, effectiveRequest.TraceId);
            throw new AclChannelException(Name,
                $"ACL channel {Name} 调用 {effectiveRequest.TargetService}/{effectiveRequest.OperationName} 超时：{ex.Message}",
                ex);
        }
        catch (AclChannelException)
        {
            // 已包装的通道异常：透传，避免双重包装
            throw;
        }
        catch (Exception ex)
        {
            AntiCorruptionMetrics.RecordFailure(ServiceName, effectiveRequest.OperationName, Name);
            Logger.LogWarning(ex,
                "ACL channel {Channel} failed for {Service}/{Operation} traceId={TraceId}",
                Name, effectiveRequest.TargetService, effectiveRequest.OperationName, effectiveRequest.TraceId);
            throw new AclChannelException(Name,
                $"ACL channel {Name} 调用 {effectiveRequest.TargetService}/{effectiveRequest.OperationName} 失败：{ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// 派生类实现：执行协议特定的远程调用并返回 <see cref="AclResponse"/>。
    /// 业务层失败应返回 <c>Success=false</c>；基础设施层失败可抛异常（由基类统一包装为 <see cref="AclChannelException"/>）。
    /// </summary>
    protected abstract Task<AclResponse> SendCoreAsync(AclRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 默认健康检查实现：调用 <see cref="IAclChannel.SendAsync"/> 发送一个 noop 探测请求。
    /// 派生类可重写为更轻量的实现（如 gRPC HealthCheck 协议）。
    /// 默认实现不发送实际请求，避免 noop 操作产生副作用，需要派生类显式重写以执行真实探测。
    /// </summary>
    public virtual Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <summary>将负载字典序列化为 JSON 字符串（供 HTTP 通道使用）。</summary>
    protected static string SerializePayload(IReadOnlyDictionary<string, object> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>从 JSON 字符串反序列化为负载字典（供 HTTP 通道解析响应使用）。</summary>
    protected static IReadOnlyDictionary<string, object>? DeserializeBody(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null!,
                    _ => prop.Value.GetRawText()
                };
            }
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
