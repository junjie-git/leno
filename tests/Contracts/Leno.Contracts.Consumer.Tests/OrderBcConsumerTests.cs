using System.Net;
using PactNet;

namespace Leno.Contracts.Consumer.Tests;

/// <summary>
/// Order BC Consumer 契约测试样例（阶段 4.10）。
///
/// 覆盖契约：Order BC → Product BC，GET /internal/v1/products/skus/{skuId}，
/// 该端点为订单下单时构建商品快照与库存校验的跨 BC 同步调用
/// （防腐层 ProductAntiCorruptionService，源码位于
/// src/Services/Order/Leno.Order.Infrastructure/Services/ProductAntiCorruptionService.cs）。
///
/// 本样例作为后续 BC 推广 Pact 契约测试的模板：
///   1. 构造 IPactBuilderV4，声明 Consumer="Order BC"、Provider="Product BC"
///   2. UponReceiving 描述交互，Given 声明 Provider 状态，WithRequest/WillRespond 固化请求与响应预期
///   3. VerifyAsync 回调中向 mock server 发起真实 HTTP 调用并断言反序列化结果
///   4. 测试通过后生成 pact 文件至仓库根 pacts/，供 Provider 测试与 Pact Broker 消费
/// </summary>
public sealed class OrderBcConsumerTests
{
    private static readonly Guid SkuId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SellerId = new("22222222-2222-2222-2222-222222222222");
    private const string InternalKey = "test-internal-key";

    private readonly IPactBuilderV4 _pactBuilder;

    public OrderBcConsumerTests()
    {
        var pact = Pact.V4("Order BC", "Product BC", new PactConfig
        {
            PactDir = PactPaths.PactDir,
        });
        _pactBuilder = pact.WithHttpInteractions();
    }

    /// <summary>
    /// 下单查询 SKU 信息：Provider 返回 ApiResponse&lt;SkuInfoResultDto&gt;，
    /// Consumer 反序列化后取 SkuId/Title/Price/Available 构建商品快照。
    /// </summary>
    [Fact]
    public async Task GetSkuInfo_WithValidId_ReturnsSkuSnapshot()
    {
        _pactBuilder
            .UponReceiving("A request to get SKU info by id")
                .Given($"A SKU with id '{SkuId}' exists")
                .WithRequest(HttpMethod.Get, $"/internal/v1/products/skus/{SkuId}")
                .WithHeader("X-Internal-Key", InternalKey)
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    code = 200,
                    message = "success",
                    data = new
                    {
                        skuId = SkuId,
                        price = 99.99m,
                        currency = "CNY",
                        available = true,
                        title = "Test SKU",
                        mainImageUrl = "https://img.example.com/sku-001.jpg",
                        sellerId = SellerId
                    }
                });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new ProductSkuConsumerClient(ctx.MockServerUri, InternalKey);
            var result = await client.GetSkuInfoAsync(SkuId);

            result.Should().NotBeNull();
            result!.SkuId.Should().Be(SkuId);
            result.Title.Should().Be("Test SKU");
            result.Price.Should().Be(99.99m);
            result.Currency.Should().Be("CNY");
            result.Available.Should().BeTrue();
            result.SellerId.Should().Be(SellerId);
            result.MainImageUrl.Should().Be("https://img.example.com/sku-001.jpg");
        });
    }

    /// <summary>
    /// SKU 不存在场景：Provider 返回 404，Consumer 防腐层将远程失败映射为业务降级。
    /// </summary>
    [Fact]
    public async Task GetSkuInfo_WhenSkuNotFound_ReturnsNull()
    {
        var missingSkuId = new Guid("33333333-3333-3333-3333-333333333333");

        _pactBuilder
            .UponReceiving("A request to get a non-existent SKU")
                .Given($"A SKU with id '{missingSkuId}' does not exist")
                .WithRequest(HttpMethod.Get, $"/internal/v1/products/skus/{missingSkuId}")
                .WithHeader("X-Internal-Key", InternalKey)
            .WillRespond()
                .WithStatus(HttpStatusCode.NotFound)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    code = 404,
                    message = "SKU 不存在",
                    data = (object?)null
                });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new ProductSkuConsumerClient(ctx.MockServerUri, InternalKey);
            var result = await client.GetSkuInfoAsync(missingSkuId);

            result.Should().BeNull();
        });
    }
}

/// <summary>
/// 解析 pact 文件输出目录到仓库根 pacts/，供 Consumer 写入、Provider 读取。
/// 通过向上查找 Leno.slnx 定位仓库根，避免依赖测试运行的工作目录。
/// </summary>
internal static class PactPaths
{
    public static string PactDir { get; } = ResolvePactDir();

    private static string ResolvePactDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Leno.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                "无法定位仓库根目录（未找到 Leno.slnx），pact 文件输出目录解析失败。");
        }

        var pactDir = Path.Combine(dir.FullName, "pacts");
        Directory.CreateDirectory(pactDir);
        return pactDir;
    }
}
