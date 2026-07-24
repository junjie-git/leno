using System.Net.Http.Headers;
using System.Text.Json;

namespace Leno.Contracts.Consumer.Tests;

/// <summary>
/// 商品域 SKU 查询 Consumer 客户端，镜像 Order BC 防腐层
/// <c>ProductAntiCorruptionService.GetSkuInfoAsync</c> 的真实调用契约：
/// GET internal/v1/products/skus/{skuId}，携带 X-Internal-Key 头，
/// 反序列化为 <see cref="ApiResponse{T}"/> 统一响应体。
/// 此客户端为 Consumer 自有实现，独立于 Provider 的 DTO，体现"契约即 JSON"的 Pact 原则。
/// </summary>
public sealed class ProductSkuConsumerClient : IDisposable
{
    private const string InternalKeyName = "X-Internal-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public ProductSkuConsumerClient(Uri baseAddress, string internalKey)
    {
        _httpClient = new HttpClient { BaseAddress = baseAddress };
        _httpClient.DefaultRequestHeaders.Add(InternalKeyName, internalKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// 查询 SKU 当前信息，镜像 Order BC 下单时构建商品快照的调用。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>SKU 信息载荷；不存在或反序列化失败返回 null。</returns>
    public async Task<SkuInfoPayload?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(
            $"internal/v1/products/skus/{skuId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<ApiResponse<SkuInfoPayload>>(stream, JsonOptions, ct);
        return payload?.Data;
    }

    public void Dispose() => _httpClient.Dispose();
}

/// <summary>
/// 统一 API 响应体，镜像 <c>Leno.SharedContracts.Responses.ApiResponse&lt;T&gt;</c> 的 JSON 形状。
/// Consumer 测试自持该结构，避免与 Provider 共享类型导致契约耦合。
/// </summary>
public sealed class ApiResponse<T>
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }
}

/// <summary>
/// SKU 信息载荷，镜像 Order BC 防腐层消费的字段子集
/// （SkuId / Price / Currency / Available / Title / MainImageUrl / SellerId）。
/// Provider 实际返回字段更多（SpuId / Stock / Status 等），但 Consumer 仅依赖此子集，
/// Pact 默认允许 Provider 返回额外字段，故子集契约不会因 Provider 新增字段而误判为破坏性变更。
/// </summary>
public sealed class SkuInfoPayload
{
    public Guid SkuId { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = string.Empty;

    public bool Available { get; set; }

    public string Title { get; set; } = string.Empty;

    public string MainImageUrl { get; set; } = string.Empty;

    public Guid SellerId { get; set; }
}
