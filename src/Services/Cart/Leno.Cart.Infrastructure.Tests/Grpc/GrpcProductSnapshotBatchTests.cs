using FluentAssertions;
using Grpc.Core;
using Leno.Cart.Infrastructure.Services.Grpc;
using Leno.Infrastructure.AntiCorruption;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Cart.Infrastructure.Tests.Grpc;

/// <summary>
/// GrpcProductSnapshotAntiCorruptionClient.GetSkuSnapshotsAsync 批量查询测试（P1-3）。
/// 验证批量 RPC 调用正确填充 int64+string 双轨字段、响应按 SkuIdStr 映射回 Guid、
/// 未命中 SKU 不出现在结果中、空入参直接返回空集合、RPC 故障包装为 AntiCorruptionException。
/// </summary>
public class GrpcProductSnapshotBatchTests
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
    public async Task GetSkuSnapshotsAsync_EmptyInput_ShouldReturnEmptyWithoutRpcCall()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var client = new GrpcProductSnapshotAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var result = await client.GetSkuSnapshotsAsync(Array.Empty<Guid>());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
        clientMock.Verify(c => c.BatchGetSkuInfoAsync(
            It.IsAny<BatchGetSkuInfoRequest>(),
            It.IsAny<Metadata>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSkuSnapshotsAsync_Success_StrMapping_ShouldReturnAllRequestedSkus()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();

        var response = new BatchGetSkuInfoResponse();
        response.Skus.Add(new SkuInfo
        {
            SkuIdStr = skuId1.ToString(),
            Title = "SKU 1",
            MainImage = "http://img1",
            PriceCents = 12999,
            Currency = "CNY",
            Salable = true
        });
        response.Skus.Add(new SkuInfo
        {
            SkuIdStr = skuId2.ToString(),
            Title = "SKU 2",
            MainImage = "http://img2",
            PriceCents = 5000,
            Currency = "CNY",
            Salable = false
        });

        clientMock.Setup(c => c.BatchGetSkuInfoAsync(
                It.IsAny<BatchGetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<BatchGetSkuInfoResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcProductSnapshotAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var result = await client.GetSkuSnapshotsAsync(new List<Guid> { skuId1, skuId2 });

        result.Should().HaveCount(2);
        var first = result.Single(s => s.SkuId == skuId1);
        first.Title.Should().Be("SKU 1");
        first.UnitPrice.Should().Be(129.99m);
        first.IsOnSale.Should().BeTrue();
        var second = result.Single(s => s.SkuId == skuId2);
        second.Title.Should().Be("SKU 2");
        second.UnitPrice.Should().Be(50m);
        second.IsOnSale.Should().BeFalse();

        // C3（2026-09-24）：int64 字段已从契约删除，请求仅填充 sku_ids_str
        clientMock.Verify(c => c.BatchGetSkuInfoAsync(
            It.Is<BatchGetSkuInfoRequest>(r =>
                r.SkuIdsStr.Contains(skuId1.ToString()) &&
                r.SkuIdsStr.Contains(skuId2.ToString()) &&
                r.SkuIdsStr.Count == 2),
            It.IsAny<Metadata>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSkuSnapshotsAsync_PartialHit_ShouldReturnOnlyMatchedSkus()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();

        // 仅返回 skuId1，skuId2 未命中（商品域批量查询可能部分缺失）
        var response = new BatchGetSkuInfoResponse();
        response.Skus.Add(new SkuInfo
        {
            SkuIdStr = skuId1.ToString(),
            Title = "Hit",
            MainImage = "http://hit",
            PriceCents = 100,
            Salable = true
        });

        clientMock.Setup(c => c.BatchGetSkuInfoAsync(
                It.IsAny<BatchGetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<BatchGetSkuInfoResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcProductSnapshotAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var result = await client.GetSkuSnapshotsAsync(new List<Guid> { skuId1, skuId2 });

        result.Should().HaveCount(1);
        result.Single().SkuId.Should().Be(skuId1);
    }

    [Fact]
    public async Task GetSkuSnapshotsAsync_StrFieldEmpty_ShouldSkipUnmatchedSku()
    {
        // C3（2026-09-24）：int64 字段已从契约删除，无 int64 回退路径；
        // 服务端未回填 sku_id_str 时该条目无法映射到请求的 Guid，按未命中跳过。
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var skuId = Guid.NewGuid();

        var response = new BatchGetSkuInfoResponse();
        response.Skus.Add(new SkuInfo
        {
            Title = "NoStrId",
            MainImage = "http://nostr",
            PriceCents = 999,
            Salable = true
        });

        clientMock.Setup(c => c.BatchGetSkuInfoAsync(
                It.IsAny<BatchGetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<BatchGetSkuInfoResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcProductSnapshotAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var result = await client.GetSkuSnapshotsAsync(new List<Guid> { skuId });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSkuSnapshotsAsync_RpcUnavailable_ShouldThrowAntiCorruptionException()
    {
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "product grpc down"));

        clientMock.Setup(c => c.BatchGetSkuInfoAsync(
                It.IsAny<BatchGetSkuInfoRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcProductSnapshotAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductSnapshotAntiCorruptionClient>.Instance);

        var act = async () => await client.GetSkuSnapshotsAsync(new List<Guid> { Guid.NewGuid() });

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("PRODUCT_UNAVAILABLE");
    }
}
