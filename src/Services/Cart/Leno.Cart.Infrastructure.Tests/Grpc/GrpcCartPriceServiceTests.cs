using FluentAssertions;
using Grpc.Core;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure.Services.Grpc;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
using Leno.Testing.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Cart.Infrastructure.Tests.Grpc;

public class GrpcCartPriceServiceTests
{
    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            UseGrpc = true,
            TargetInternalApiKeys = new Dictionary<string, string> { { "Product", "test-key" } }
        });
        return mock.Object;
    }

    [Fact]
    public async Task GetSkuPrices_Success_ReturnsMappedSnapshots()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var batchResponse = new BatchGetSkuInfoResponse();
        batchResponse.Skus.Add(new SkuInfo
        {
            SkuId = (long)skuId.GetHashCode(),
            Title = "Test SKU",
            PriceCents = 9999,
            Currency = "CNY",
            Salable = true,
            Stock = 100,
            MainImage = "http://img",
            // M4 Guid→string 迁移：服务端填充 string 字段（修复 SellerId 映射验证）
            SkuIdStr = skuId.ToString(),
            SellerIdStr = sellerId.ToString()
        });

        clientMock.Setup(c => c.BatchGetSkuInfoAsync(
                It.IsAny<BatchGetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<BatchGetSkuInfoResponse>(
                Task.FromResult(batchResponse),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcCartPriceService(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcCartPriceService>.Instance);

        var result = await client.GetSkuPricesAsync(new[] { skuId });

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].SkuId.Should().Be(skuId);
        // 验证 SellerId 修复：之前是 Guid.Empty 占位，现在正确解析 string
        result[0].SellerId.Should().Be(sellerId);
        result[0].Title.Should().Be("Test SKU");
        result[0].Price.Should().Be(99.99m);
        result[0].Available.Should().BeTrue();
    }

    [Fact]
    public async Task GetSkuPrices_NewServer_OnlyString_ReturnsCorrectGuid()
    {
        // 新服务端仅填充 string 字段，int64 字段为默认值 0
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var batchResponse = new BatchGetSkuInfoResponse();
        batchResponse.Skus.Add(new SkuInfo
        {
            SkuId = 0,  // 新服务端不填充 int64
            Title = "New Server SKU",
            PriceCents = 5000,
            Currency = "CNY",
            Salable = true,
            Stock = 50,
            MainImage = "http://img2",
            SkuIdStr = skuId.ToString(),
            SellerIdStr = sellerId.ToString()
        });

        clientMock.Setup(c => c.BatchGetSkuInfoAsync(
                It.IsAny<BatchGetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<BatchGetSkuInfoResponse>(
                Task.FromResult(batchResponse),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcCartPriceService(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcCartPriceService>.Instance);

        var result = await client.GetSkuPricesAsync(new[] { skuId });

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].SkuId.Should().Be(skuId);
        result[0].SellerId.Should().Be(sellerId);
        result[0].Title.Should().Be("New Server SKU");
        result[0].Price.Should().Be(50m);
    }

    [Fact]
    public async Task GetSkuPrices_Unavailable_ThrowsAntiCorruptionException_WithRpcInner()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.BatchGetSkuInfoAsync(
                It.IsAny<BatchGetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcCartPriceService(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcCartPriceService>.Instance);

        var act = async () => await client.GetSkuPricesAsync(new[] { Guid.NewGuid() });

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("PRODUCT_UNAVAILABLE");
    }

    [Fact]
    public async Task GetSkuPrices_EmptyInput_ReturnsEmptyList_WithoutRpcCall()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>(MockBehavior.Strict);

        var client = new GrpcCartPriceService(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcCartPriceService>.Instance);

        var result = await client.GetSkuPricesAsync(Array.Empty<Guid>());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
