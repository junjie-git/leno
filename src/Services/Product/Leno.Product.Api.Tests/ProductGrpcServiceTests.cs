using Grpc.Core;
using Leno.Product.Api.GrpcServices;
using Leno.Product.Application;
using Leno.SharedContracts.Grpc.Product.V1;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Product.Api.Tests;

/// <summary>
/// ProductGrpcService 单元测试，验证 int64 字段使用稳定算法（Guid 前 8 字节）而非 GetHashCode（审计 #5）。
/// </summary>
public class ProductGrpcServiceTests
{
    [Fact]
    public async Task GetSkuInfo_Int64_Field_Should_Use_Stable_Mapping_Not_GetHashCode()
    {
        // Arrange
        var skuId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        var expectedInt64 = BitConverter.ToInt64(skuId.ToByteArray(), 0);

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

        // Assert：int64 字段应使用稳定算法（前 8 字节），而非 GetHashCode
        result.SkuId.Should().Be(expectedInt64);
        result.SkuId.Should().NotBe((long)skuId.GetHashCode());
        result.SkuIdStr.Should().Be(skuId.ToString());
    }

    [Fact]
    public async Task GetSkuInfo_Int64_Field_Should_Be_Deterministic_For_Same_Guid()
    {
        // Arrange
        var skuId = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");
        var expectedInt64 = BitConverter.ToInt64(skuId.ToByteArray(), 0);

        var dto1 = new SkuInfoResultDto
        {
            SkuId = skuId,
            SpuId = Guid.NewGuid(),
            Price = 10m,
            Currency = "CNY",
            Stock = 50,
            Status = "active",
            Title = "SKU1",
            MainImageUrl = "https://cdn.example.com/1.png",
            SellerId = Guid.NewGuid(),
            ShopId = Guid.NewGuid()
        };

        var mockQueryService = new Mock<IProductInternalQueryService>();
        mockQueryService
            .Setup(s => s.GetSkuInfoAsync(skuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto1);

        var logger = new Mock<ILogger<ProductGrpcService>>();
        var service = new ProductGrpcService(mockQueryService.Object, logger.Object);

        // Act：两次调用相同 Guid
        var request1 = new GetSkuInfoRequest { SkuIdStr = skuId.ToString() };
        var context = CreateServerCallContext();
        var result1 = await service.GetSkuInfo(request1, context);

        var request2 = new GetSkuInfoRequest { SkuIdStr = skuId.ToString() };
        var result2 = await service.GetSkuInfo(request2, context);

        // Assert：相同 Guid 产生相同 int64（确定性）
        result1.SkuId.Should().Be(result2.SkuId);
        result1.SkuId.Should().Be(expectedInt64);
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
