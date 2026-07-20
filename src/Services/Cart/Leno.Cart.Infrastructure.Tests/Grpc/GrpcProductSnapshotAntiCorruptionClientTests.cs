using FluentAssertions;
using Grpc.Core;
using Leno.Cart.Infrastructure.Services.Grpc;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
using Leno.Testing.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Cart.Infrastructure.Tests.Grpc;

public class GrpcProductSnapshotAntiCorruptionClientTests
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
    public async Task GetSkuSnapshot_Success_ReturnsMappedSnapshot()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var skuId = Guid.NewGuid();
        var skuInfo = new SkuInfo
        {
            SkuId = (long)skuId.GetHashCode(),
            Title = "Test SKU",
            MainImage = "http://img",
            PriceCents = 12999,
            Currency = "CNY",
            Salable = true,
            Stock = 100
        };

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<SkuInfo>(
                Task.FromResult(skuInfo),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcProductSnapshotAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var result = await client.GetSkuSnapshotAsync(skuId);

        result.Should().NotBeNull();
        result.SkuId.Should().Be(skuId);
        result.Title.Should().Be("Test SKU");
        result.MainImageUrl.Should().Be("http://img");
        result.UnitPrice.Should().Be(129.99m);
        result.IsOnSale.Should().BeTrue();
    }

    [Fact]
    public async Task GetSkuSnapshot_Unavailable_ThrowsAntiCorruptionException_WithRpcInner()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcProductSnapshotAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var act = async () => await client.GetSkuSnapshotAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("PRODUCT_UNAVAILABLE");
    }

    [Fact]
    public async Task GetSkuSnapshot_NotFound_ThrowsAntiCorruptionException_RemoteFailed()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "sku not found"));

        clientMock.Setup(c => c.GetSkuInfoAsync(
                It.IsAny<GetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcProductSnapshotAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var act = async () => await client.GetSkuSnapshotAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.ErrorCode.Should().Be("PRODUCT_REMOTE_FAILED");
    }
}
