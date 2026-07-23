using System.Net;

namespace Leno.Identity.Application.Tests.OAuth;

/// <summary>
/// 测试用 HttpMessageHandler，按请求 URL 返回预设响应。
/// 用于在不依赖真实 IdP 的情况下测试 OIDC 适配器的 discovery / token / userinfo 流程。
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Content)> _responses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<HttpRequestMessage> _requests = new();

    /// <summary>按 URL 子串匹配注册响应。键可为完整 URL 或 URL 片段。</summary>
    public void Register(string urlContains, HttpStatusCode status, string content)
    {
        _responses[urlContains] = (status, content);
    }

    /// <summary>记录的所有出站请求，供测试断言。</summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);

        var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
        foreach (var kv in _responses)
        {
            if (url.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(kv.Value.Status)
                {
                    Content = new StringContent(kv.Value.Content)
                });
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No fake response registered for {url}")
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var req in _requests)
            {
                req.Dispose();
            }
            _requests.Clear();
        }
        base.Dispose(disposing);
    }
}
