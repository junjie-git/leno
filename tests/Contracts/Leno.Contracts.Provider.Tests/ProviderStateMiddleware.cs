using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Leno.Contracts.Provider.Tests;

/// <summary>
/// Provider 状态设置中间件（阶段 4.10）。
/// Pact Verifier 在验证每个交互前，向 /provider-states 发送 POST 请求，body 含
/// action（setup/teardown）与 state（Consumer 在 Given 中声明的状态描述）。
/// 本中间件据此向 <see cref="InMemorySkuStore"/> 注入或清理测试数据，
/// 使 Provider 端点能返回与契约一致的响应。
/// </summary>
public sealed class ProviderStateMiddleware
{
    private const string ProviderStatesPath = "/provider-states";
    private const string SetupAction = "setup";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly InMemorySkuStore _store;

    public ProviderStateMiddleware(RequestDelegate next, InMemorySkuStore store)
    {
        _next = next;
        _store = store;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(ProviderStatesPath, out var remaining)
            && !remaining.HasValue)
        {
            await HandleProviderStateAsync(context);
            return;
        }

        await _next(context);
    }

    private async Task HandleProviderStateAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return;
        }

        var request = await context.Request.ReadFromJsonAsync<ProviderStateRequest>(JsonOptions, context.RequestAborted);
        if (request is null)
        {
            return;
        }

        var action = string.IsNullOrWhiteSpace(request.Action) ? SetupAction : request.Action!;
        var state = ExtractState(request);

        if (action == SetupAction && state is not null)
        {
            await SetupProviderStateAsync(state);
        }
    }

    private static string? ExtractState(ProviderStateRequest request)
    {
        // 兼容 Pact V2（state 字符串）与 V3/V4（states 数组）两种 provider state 格式
        if (!string.IsNullOrWhiteSpace(request.State))
        {
            return request.State;
        }

        if (request.States is { Count: > 0 })
        {
            return request.States[0].Name;
        }

        return null;
    }

    private Task SetupProviderStateAsync(string state)
    {
        _store.Clear();

        var skuId = TryExtractSkuId(state);
        if (skuId is null)
        {
            // 未识别的状态描述：保持 store 为空（如 "does not exist" 场景）
            return Task.CompletedTask;
        }

        if (state.Contains("does not exist", StringComparison.Ordinal))
        {
            // 显式声明不存在的 SKU：无需 seed，store 已清空
            return Task.CompletedTask;
        }

        // seed 与 Consumer 契约预期一致的 SKU 测试数据
        _store.Seed(SkuTestFixtures.CreateSku(skuId.Value));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 从状态描述 "A SKU with id '{guid}' exists" 中解析 SKU 标识。
    /// </summary>
    private static Guid? TryExtractSkuId(string state)
    {
        var firstQuote = state.IndexOf('\'');
        if (firstQuote < 0)
        {
            return null;
        }

        var secondQuote = state.IndexOf('\'', firstQuote + 1);
        if (secondQuote < 0)
        {
            return null;
        }

        var idStr = state.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}

/// <summary>
/// Pact Verifier 发送的 provider state 请求体。
/// 兼容 V2（State 字符串）与 V3+（States 数组）格式。
/// </summary>
public sealed class ProviderStateRequest
{
    public string? Action { get; set; }

    public string? State { get; set; }

    public List<ProviderStateItem>? States { get; set; }

    public string? Consumer { get; set; }
}

public sealed class ProviderStateItem
{
    public string Name { get; set; } = string.Empty;
}
