using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Leno.ApiGateway.Bff.Models;
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Bff;

/// <summary>
/// <see cref="IBffForwarderService"/> 默认实现：基于 <see cref="IHttpClientFactory"/> 与
/// <see cref="Parallel.ForEachAsync"/> 的并行下游聚合器。
/// <para>
/// 实现要点：
/// <list type="bullet">
///   <item>整体超时（默认 10 秒，<see cref="DefaultOverallTimeout"/>），由 linked CTS 控制</item>
///   <item>每个下游请求独立超时（默认 3 秒，<see cref="DefaultPerRequestTimeout"/>），失败仅影响该请求</item>
///   <item>使用 <see cref="Parallel.ForEachAsync"/> 并行调度，MaxDegreeOfParallelism=requests.Count</item>
///   <item>响应以 <see cref="JsonElement"/>? 形式存入并发字典（成功）或 <see cref="BffError"/> 存入并发字典（失败，T16 改用 ConcurrentDictionary 原子去重）</item>
///   <item>HTTP 200 + Partial=true 表示部分成功，调用方通过 Errors 字段识别失败来源</item>
/// </list>
/// </para>
/// </summary>
public sealed class BffForwarderService : IBffForwarderService
{
    /// <summary>下游调用的默认单请求超时（3 秒）。</summary>
    internal static readonly TimeSpan DefaultPerRequestTimeout = TimeSpan.FromSeconds(3);

    /// <summary>下游调用的默认整体超时（10 秒），应大于单请求超时。</summary>
    internal static readonly TimeSpan DefaultOverallTimeout = TimeSpan.FromSeconds(10);

    /// <summary>向后兼容：下游调用的默认超时（3 秒），等价于 <see cref="DefaultPerRequestTimeout"/>。</summary>
    internal static readonly TimeSpan DefaultTimeout = DefaultPerRequestTimeout;

    internal const string HttpClientName = "BffForwarder";
    private const string RequestIdHeader = "X-Request-Id";
    private const string JsonContentType = "application/json";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeSpan _overallTimeout;
    private readonly TimeSpan _perRequestTimeout;

    /// <summary>公开构造函数：使用默认超时（整体 10s、单请求 3s）。</summary>
    public BffForwarderService(IHttpClientFactory httpClientFactory)
        : this(httpClientFactory, DefaultOverallTimeout, DefaultPerRequestTimeout)
    {
    }

    /// <summary>
    /// 公开构造函数：从 <see cref="BffOptions"/> 读取整体与单请求超时。
    /// T15：区分整体超时与单请求超时，整体超时应大于单请求超时。
    /// </summary>
    public BffForwarderService(IHttpClientFactory httpClientFactory, IOptions<BffOptions> options)
        : this(
              httpClientFactory,
              options?.Value?.OverallTimeout ?? DefaultOverallTimeout,
              options?.Value?.PerRequestTimeout ?? DefaultPerRequestTimeout)
    {
    }

    /// <summary>测试用构造函数：允许覆盖超时以加速超时场景测试（整体与单请求使用同一超时，向后兼容）。</summary>
    internal BffForwarderService(IHttpClientFactory httpClientFactory, TimeSpan timeout)
        : this(httpClientFactory, timeout, timeout)
    {
    }

    /// <summary>测试用构造函数：允许分别覆盖整体与单请求超时。</summary>
    internal BffForwarderService(IHttpClientFactory httpClientFactory, TimeSpan overallTimeout, TimeSpan perRequestTimeout)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        if (overallTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(overallTimeout), "整体超时必须为正值");
        }
        if (perRequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(perRequestTimeout), "单请求超时必须为正值");
        }
        _overallTimeout = overallTimeout;
        _perRequestTimeout = perRequestTimeout;
    }

    /// <inheritdoc />
    public async Task<BffResponse<T>> ForwardAsync<T>(
        string requestId,
        IReadOnlyList<BffDownstreamRequest> requests,
        Func<IReadOnlyDictionary<string, JsonElement?>, T> aggregator,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(aggregator);
        if (requests.Count == 0)
        {
            throw new ArgumentException("至少需要 1 个下游请求", nameof(requests));
        }

        var results = new ConcurrentDictionary<string, JsonElement?>();
        // T16：改用 ConcurrentDictionary<string, BffError> 替代 ConcurrentBag<BffError>，
        // 以 Source 为键实现原子去重（TryAdd），避免整体超时回填 504 时产生重复错误条目
        var errors = new ConcurrentDictionary<string, BffError>();

        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        overallCts.CancelAfter(_overallTimeout);

        try
        {
            await Parallel.ForEachAsync(
                requests,
                new ParallelOptions
                {
                    CancellationToken = overallCts.Token,
                    MaxDegreeOfParallelism = requests.Count
                },
                async (req, token) =>
                {
                    using var perRequestCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    perRequestCts.CancelAfter(_perRequestTimeout);

                    try
                    {
                        await SendDownstreamAsync(req, requestId, results, perRequestCts.Token);
                    }
                    catch (DownstreamFailureException dfe)
                    {
                        // 下游返回非 2xx：保留原始 StatusCode 与响应体摘要
                        // T16：TryAdd 原子去重，同一 Source 仅保留第一个错误
                        errors.TryAdd(dfe.Source, new BffError
                        {
                            Source = dfe.Source,
                            StatusCode = dfe.StatusCode,
                            Message = dfe.Message
                        });
                    }
                    catch (OperationCanceledException) when (perRequestCts.IsCancellationRequested && !token.IsCancellationRequested)
                    {
                        // 单请求超时（perRequestCts 触发，但整体 token 仍在有效期内）
                        errors.TryAdd(req.Source, new BffError
                        {
                            Source = req.Source,
                            StatusCode = 504,
                            Message = "Request timed out"
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        // 整体 token 取消，由外层 catch 统一记录为 504
                    }
                    catch (HttpRequestException ex)
                    {
                        errors.TryAdd(req.Source, new BffError
                        {
                            Source = req.Source,
                            StatusCode = 503,
                            Message = ex.Message
                        });
                    }
                    catch (Exception ex)
                    {
                        errors.TryAdd(req.Source, new BffError
                        {
                            Source = req.Source,
                            StatusCode = 500,
                            Message = ex.Message
                        });
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (overallCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // 整体超时：将尚未产生结果的请求标记为 504
            // T16：ConcurrentDictionary.TryAdd 原子去重，无需手动遍历 errors 检查已存在条目
            foreach (var req in requests)
            {
                if (results.ContainsKey(req.Source))
                {
                    continue;
                }

                errors.TryAdd(req.Source, new BffError
                {
                    Source = req.Source,
                    StatusCode = 504,
                    Message = $"Overall timeout ({_overallTimeout.TotalSeconds:F0}s)"
                });
            }
        }

        T? aggregated = default;
        if (!results.IsEmpty)
        {
            try
            {
                aggregated = aggregator(results);
            }
            catch (Exception)
            {
                // 聚合失败不破坏整体响应，仅 Data 为 null
                aggregated = null;
            }
        }

        return new BffResponse<T>
        {
            Success = errors.IsEmpty,
            Partial = !errors.IsEmpty && !results.IsEmpty,
            Data = aggregated,
            Errors = errors.Values.ToArray()
        };
    }

    private async Task SendDownstreamAsync(
        BffDownstreamRequest req,
        string requestId,
        ConcurrentDictionary<string, JsonElement?> results,
        CancellationToken token)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var requestMessage = new HttpRequestMessage(
            new HttpMethod(req.Method ?? "GET"),
            req.ServiceUrl);

        if (!string.IsNullOrEmpty(requestId))
        {
            requestMessage.Headers.TryAddWithoutValidation(RequestIdHeader, requestId);
        }

        var method = requestMessage.Method;
        if (!string.IsNullOrEmpty(req.RequestBody)
            && method != HttpMethod.Get
            && method != HttpMethod.Head
            && method != HttpMethod.Delete)
        {
            requestMessage.Content = new StringContent(req.RequestBody, Encoding.UTF8);
            requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonContentType);
        }

        using var response = await client.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            token).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            // ReadFromJsonAsync<JsonElement> 内部调用 JsonElementConverter，
            // 该 converter 通过 RootElement.Clone() 返回自包含 JsonElement，可安全跨 async 边界持有
            var json = await response.Content
                .ReadFromJsonAsync<JsonElement>(cancellationToken: token)
                .ConfigureAwait(false);
            results[req.Source] = json;
            return;
        }

        var body = await response.Content
            .ReadAsStringAsync(token)
            .ConfigureAwait(false);
        if (body.Length > 512)
        {
            body = body[..512] + "...";
        }

        // 下游非 2xx：抛出携带原始 StatusCode 与响应体摘要的异常，由调用方 catch 转为 BffError
        throw new DownstreamFailureException(req.Source, (int)response.StatusCode, body);
    }

    /// <summary>
    /// 下游非 2xx 响应时抛出，供外层 catch 转换为 <see cref="BffError"/>。
    /// </summary>
    private sealed class DownstreamFailureException : Exception
    {
        public DownstreamFailureException(string source, int statusCode, string message)
            : base(message)
        {
            Source = source;
            StatusCode = statusCode;
        }

        public new string Source { get; }

        public int StatusCode { get; }
    }
}
