using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Order.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Grpc;

public class GrpcProductAntiCorruptionClientTests
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
    public async Task GetSkuInfo_Success_ReturnsMappedDto()
    {
        // ProductInternalServiceClient 有 protected 无参构造函数，Moq 可直接 mock
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var skuInfoProto = new SkuInfo
        {
            SkuId = 123,
            SpuId = 456,
            Title = "Test SKU",
            PriceCents = 9999,
            Stock = 100,
            Salable = true,
            SellerId = 789,
            Status = "active",
            Currency = "CNY",
            MainImage = "http://img",
            ShopId = Guid.NewGuid().ToString(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<SkuInfo>(
                Task.FromResult(skuInfoProto),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcProductAntiCorruptionClient(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductAntiCorruptionClient>.Instance);

        var result = await client.GetSkuInfoAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.ProductName.Should().Be("Test SKU");
        result.UnitPrice.Should().Be(99.99m);
        result.AvailableQty.Should().Be(100);
    }

    [Fact]
    public async Task GetSkuInfo_Unavailable_ThrowsAntiCorruptionException_WithRpcInner()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcProductAntiCorruptionClient(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductAntiCorruptionClient>.Instance);

        var act = async () => await client.GetSkuInfoAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("PRODUCT_UNAVAILABLE");
    }

    [Fact]
    public async Task GetSkuInfo_NotFound_ThrowsAntiCorruptionException_RemoteFailed()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "sku missing"));

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcProductAntiCorruptionClient(clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductAntiCorruptionClient>.Instance);

        var act = async () => await client.GetSkuInfoAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.ErrorCode.Should().Be("PRODUCT_REMOTE_FAILED");
    }
}
