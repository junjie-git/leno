using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.SellerShop.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Order.V1;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.SellerShop.Infrastructure.Tests;

/// <summary>
/// 卖家店铺域防腐层 gRPC 客户端单元测试。
/// 覆盖 GetSpuSellerIdAsync / GetOrderSellerIdAsync 的成功与失败两个分支：
/// 成功时返回正确的 SellerId；失败时（gRPC 不可达/业务异常）fail-closed 返回 null。
/// </summary>
public class SellerShopAntiCorruptionTests
{
    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            UseGrpc = true,
            TargetInternalApiKeys = new Dictionary<string, string>
            {
                { "Product", "test-key-product" },
                { "Order", "test-key-order" }
            }
        });
        return mock.Object;
    }

    private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
        => new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    [Fact]
    public async Task GetSpuSellerId_GrpcReturnsValid_ReturnsSellerId()
    {
        // 安排：mock gRPC 客户端返回带 SellerIdStr 的 ProductDetail
        var spuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var productDetail = new ProductDetail
        {
            SpuIdStr = spuId.ToString(),
            SellerIdStr = sellerId.ToString(),
            Title = "测试商品"
        };
        clientMock.Setup(c => c.GetProductDetailAsync(
                It.IsAny<GetProductDetailRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(productDetail));

        var client = new GrpcProductAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductAntiCorruptionClient>.Instance);

        // 行动
        var result = await client.GetSpuSellerIdAsync(spuId, CancellationToken.None);

        // 断言：返回 SellerId（解析自 SellerIdStr）
        result.Should().Be(sellerId);
    }

    [Fact]
    public async Task GetSpuSellerId_GrpcFailure_ReturnsNull()
    {
        // 安排：mock gRPC 客户端抛 Unavailable 异常
        var spuId = Guid.NewGuid();
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        clientMock.Setup(c => c.GetProductDetailAsync(
                It.IsAny<GetProductDetailRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "gRPC down")));

        var client = new GrpcProductAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductAntiCorruptionClient>.Instance);

        // 行动
        var result = await client.GetSpuSellerIdAsync(spuId, CancellationToken.None);

        // 断言：fail-closed 返回 null
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSpuSellerId_GrpcReturnsEmptySellerIdStr_ReturnsNull()
    {
        // 安排：mock gRPC 客户端返回空 SellerIdStr（商品不存在或未关联卖家）
        var spuId = Guid.NewGuid();
        var clientMock = new Mock<ProductInternalService.ProductInternalServiceClient>();
        var productDetail = new ProductDetail
        {
            SpuIdStr = spuId.ToString(),
            SellerIdStr = string.Empty,  // 商品未关联卖家
            Title = "无卖家商品"
        };
        clientMock.Setup(c => c.GetProductDetailAsync(
                It.IsAny<GetProductDetailRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(productDetail));

        var client = new GrpcProductAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcProductAntiCorruptionClient>.Instance);

        // 行动
        var result = await client.GetSpuSellerIdAsync(spuId, CancellationToken.None);

        // 断言：SellerIdStr 为空时 Guid.TryParse 失败，返回 null
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderSellerId_GrpcReturnsValid_ReturnsSellerId()
    {
        // 安排：mock gRPC 客户端返回带 SellerIdStr 的 GetOrderSellerIdResponse
        var orderId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var clientMock = new Mock<OrderInternalService.OrderInternalServiceClient>();
        var response = new GetOrderSellerIdResponse
        {
            SellerIdStr = sellerId.ToString()
        };
        clientMock.Setup(c => c.GetOrderSellerIdAsync(
                It.IsAny<GetOrderSellerIdRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(response));

        var client = new GrpcOrderAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcOrderAntiCorruptionClient>.Instance);

        // 行动
        var result = await client.GetOrderSellerIdAsync(orderId, CancellationToken.None);

        // 断言：返回 SellerId（解析自 SellerIdStr）
        result.Should().Be(sellerId);
    }

    [Fact]
    public async Task GetOrderSellerId_GrpcFailure_ReturnsNull()
    {
        // 安排：mock gRPC 客户端抛 Unavailable 异常
        var orderId = Guid.NewGuid();
        var clientMock = new Mock<OrderInternalService.OrderInternalServiceClient>();
        clientMock.Setup(c => c.GetOrderSellerIdAsync(
                It.IsAny<GetOrderSellerIdRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "gRPC down")));

        var client = new GrpcOrderAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcOrderAntiCorruptionClient>.Instance);

        // 行动
        var result = await client.GetOrderSellerIdAsync(orderId, CancellationToken.None);

        // 断言：fail-closed 返回 null
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderSellerId_GrpcReturnsNotFound_ReturnsNull()
    {
        // 安排：mock gRPC 客户端抛 NotFound 异常（订单不存在）
        var orderId = Guid.NewGuid();
        var clientMock = new Mock<OrderInternalService.OrderInternalServiceClient>();
        clientMock.Setup(c => c.GetOrderSellerIdAsync(
                It.IsAny<GetOrderSellerIdRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.NotFound, "order not found")));

        var client = new GrpcOrderAntiCorruptionClient(
            clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcOrderAntiCorruptionClient>.Instance);

        // 行动
        var result = await client.GetOrderSellerIdAsync(orderId, CancellationToken.None);

        // 断言：fail-closed 返回 null（资源不存在即不归属）
        result.Should().BeNull();
    }
}
