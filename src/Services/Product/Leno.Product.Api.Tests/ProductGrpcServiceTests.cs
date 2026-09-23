using Grpc.Core;
using Leno.Product.Api.GrpcServices;
using Leno.Product.Application;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Product.Api.Tests;

/// <summary>
/// ProductGrpcService 单元测试：C3 后标识字段仅 string 形态（int64 兼容字段已从契约删除）。
/// </summary>
public class ProductGrpcServiceTests
{
    [Fact]
    public async Task GetSkuInfo_Should_Return_String_SkuId_Only()
    {
        // C3（2026-09-24）：int64 兼容字段已从契约删除，响应仅含 string 形态
        // Arrange
        var skuId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        var dto = new SkuInfoResultDto
        {
            SkuId = skuId,
            SpuId = Guid.NewGuid(),
            Price = 19.99m,
            Currency = "CNY",
            Stock = 100,
            Status = "active",
            Title = "测试 SKU",
            MainImageUrl = "https://cdn.example.com/sku.png",
            SellerId = Guid.NewGuid(),
            ShopId = Guid.NewGuid()
        };

        var mockQueryService = new Mock<IProductInternalQueryService>();
        mockQueryService
            .Setup(s => s.GetSkuInfoAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var logger = new Mock<ILogger<ProductGrpcService>>();
        var service = new ProductGrpcService(mockQueryService.Object, logger.Object);

        var request = new GetSkuInfoRequest { SkuIdStr = skuId.ToString() };
        var context = CreateServerCallContext();

        // Act
        var result = await service.GetSkuInfo(request, context);

        // Assert
        result.SkuIdStr.Should().Be(skuId.ToString());
    }

    [Fact]
    public async Task GetSkuInfo_PriceCents_Should_Round_Not_Truncate()
    {
        // Arrange：19.99 * 100 = 1999，截断会得到 1998（浮点误差），四舍五入得 1999
        var skuId = Guid.NewGuid();
        var dto = new SkuInfoResultDto
        {
            SkuId = skuId,
            SpuId = Guid.NewGuid(),
            Price = 19.99m,
            Currency = "CNY",
            Stock = 100,
            Status = "active",
            Title = "测试 SKU",
            MainImageUrl = "https://cdn.example.com/sku.png",
            SellerId = Guid.NewGuid(),
            ShopId = Guid.NewGuid()
        };

        var mockQueryService = new Mock<IProductInternalQueryService>();
        mockQueryService
            .Setup(s => s.GetSkuInfoAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var logger = new Mock<ILogger<ProductGrpcService>>();
        var service = new ProductGrpcService(mockQueryService.Object, logger.Object);

        var request = new GetSkuInfoRequest { SkuIdStr = skuId.ToString() };
        var context = CreateServerCallContext();

        // Act
        var result = await service.GetSkuInfo(request, context);

        // Assert：19.99 元应映射为 1999 分，而非截断为 1998
        result.PriceCents.Should().Be(1999);
    }

    private static ServerCallContext CreateServerCallContext() => new TestServerCallContext();

    /// <summary>
    /// gRPC 服务端单元测试用 <see cref="ServerCallContext"/> 最小实现。
    /// 仅满足 ProductGrpcService 直接调用所需成员，不涉及网络/调度。
    /// 与 Promotion / SellerShop / Cart / ReviewAfterSales 域 TestServerCallContext 风格保持一致。
    /// </summary>
    private sealed class TestServerCallContext : ServerCallContext
    {
        protected override string MethodCore => "/test/Method";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore { get; } = new();
        protected override Metadata ResponseTrailersCore { get; } = new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override AuthContext AuthContextCore
            => new AuthContext(null, new Dictionary<string, List<AuthProperty>>());
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            => Task.CompletedTask;
    }
}
