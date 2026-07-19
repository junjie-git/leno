using FluentAssertions;
using Grpc.Core;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure.Services.Grpc;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
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
        var batchResponse = new BatchGetSkuInfoResponse();
        batchResponse.Skus.Add(new SkuInfo
        {
            SkuId = (long)skuId.GetHashCode(),
            Title = "Test SKU",
            PriceCents = 9999,
            Currency = "CNY",
            Salable = true,
            Stock = 100,
            MainImage = "http://img"
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

        var client = new GrpcCartPriceService(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcCartPriceService>.Instance);

        var result = await client.GetSkuPricesAsync(new[] { skuId });

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].SkuId.Should().Be(skuId);
        result[0].Title.Should().Be("Test SKU");
        result[0].Price.Should().Be(99.99m);
        result[0].Available.Should().BeTrue();
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

        var client = new GrpcCartPriceService(clientMock.Object, CreateOptionsMonitor(),
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

        var client = new GrpcCartPriceService(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcCartPriceService>.Instance);

        var result = await client.GetSkuPricesAsync(Array.Empty<Guid>());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
