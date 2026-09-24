using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PactNet;

namespace Leno.Contracts.Consumer.Tests;

/// <summary>
/// Cart BC Consumer 契约测试（P1#16 契约推广第 2 条）。
///
/// 覆盖契约：Cart BC → Product BC，POST /internal/v1/products/skus/batch，
/// 购物车价格/可售性批量查询的跨 BC 同步调用
/// （防腐层 CartPriceService，源码位于
/// src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs）。
/// 复用 OrderBcConsumerTests 的模板结构：声明交互 → VerifyAsync 内真实 HTTP 调用 →
/// 生成 pact 文件至仓库根 pacts/Cart BC-Product BC.json。
/// </summary>
public sealed class CartBcConsumerTests
{
    private static readonly Guid SkuId1 = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SkuId2 = new("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SellerId = new("22222222-2222-2222-2222-222222222222");
    private const string InternalKey = "test-internal-key";

    private readonly IPactBuilderV4 _pactBuilder;

    public CartBcConsumerTests()
    {
        var pact = Pact.V4("Cart BC", "Product BC", new PactConfig
        {
            PactDir = PactPaths.PactDir,
        });
        _pactBuilder = pact.WithHttpInteractions();
    }

    /// <summary>
    /// 购物车结算预览批量查询 SKU 价格与可售状态：
    /// 请求体为 SKU 标识数组，Provider 返回 ApiResponse&lt;List&lt;SkuInfo&gt;&gt;。
    /// </summary>
    [Fact]
    public async Task GetSkuPrices_WithMultipleIds_ReturnsPriceSnapshots()
    {
        _pactBuilder
            .UponReceiving("A batch price query for two SKUs")
                .Given($"A SKU with id '{SkuId1}' exists")
                .Given($"A SKU with id '{SkuId2}' exists")
                .WithRequest(HttpMethod.Post, "/internal/v1/products/skus/batch")
                .WithHeader("X-Internal-Key", InternalKey)
                .WithJsonBody(new[] { SkuId1, SkuId2 })
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    code = 200,
                    message = "success",
                    data = new[]
                    {
                        new
                        {
                            skuId = SkuId1,
                            price = 99.99m,
                            currency = "CNY",
                            available = true,
                            title = "Test SKU",
                            mainImageUrl = "https://img.example.com/sku-001.jpg",
                            sellerId = SellerId
                        },
                        new
                        {
                            skuId = SkuId2,
                            price = 99.99m,
                            currency = "CNY",
                            available = true,
                            title = "Test SKU",
                            mainImageUrl = "https://img.example.com/sku-001.jpg",
                            sellerId = SellerId
                        }
                    }
                });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new CartPriceConsumerClient(ctx.MockServerUri, InternalKey);
            var result = await client.GetSkuPricesAsync(new[] { SkuId1, SkuId2 });

            result.Should().HaveCount(2);
            result[0].SkuId.Should().Be(SkuId1);
            result[0].Price.Should().Be(99.99m);
            result[0].Currency.Should().Be("CNY");
            result[0].Available.Should().BeTrue();
            result[1].SkuId.Should().Be(SkuId2);
            result[1].Price.Should().Be(99.99m);
        });
    }
}

/// <summary>
/// 商品域 SKU 批量查价 Consumer 客户端，镜像 Cart BC 防腐层
/// <c>CartPriceService.GetSkuPricesAsync</c> 的真实调用契约：
/// POST internal/v1/products/skus/batch，body 为 SKU 标识数组，携带 X-Internal-Key 头。
/// </summary>
public sealed class CartPriceConsumerClient : IDisposable
{
    private const string InternalKeyName = "X-Internal-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public CartPriceConsumerClient(Uri baseAddress, string internalKey)
    {
        _httpClient = new HttpClient { BaseAddress = baseAddress };
        _httpClient.DefaultRequestHeaders.Add(InternalKeyName, internalKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// 批量查询 SKU 价格快照。
    /// </summary>
    public async Task<List<SkuInfoPayload>> GetSkuPricesAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(skuIds, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("internal/v1/products/skus/batch", content, ct);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<ApiResponse<List<SkuInfoPayload>>>(stream, JsonOptions, ct);
        return payload?.Data ?? [];
    }

    public void Dispose() => _httpClient.Dispose();
}
