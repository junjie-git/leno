using Leno.Infrastructure.AntiCorruption;
using Leno.ReviewAfterSales.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.Order.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Infrastructure;

/// <summary>
/// 审计 3.5：GrpcOrderStatusProvider 返回 OrderLineId=Guid.Empty 且 SkuId 可能丢失。
/// 验证 proto 缺关键字段时抛 AntiCorruptionException 而非静默 Guid.Empty。
/// </summary>
public sealed class GrpcOrderStatusProviderMappingTests
{
    private static GrpcOrderStatusProvider CreateProvider(out OrderInternalService.OrderInternalServiceClient clientMock)
    {
        var mock = new Mock<OrderInternalService.OrderInternalServiceClient>(MockBehavior.Strict);
        clientMock = mock.Object;

        var options = Options.Create(new AntiCorruptionOptions
        {
            TargetInternalApiKeys = new Dictionary<string, string> { ["Order"] = "test-key" }
        });
        var optionsMonitor = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        optionsMonitor.SetupGet(o => o.CurrentValue).Returns(options.Value);

        return new GrpcOrderStatusProvider(mock.Object, optionsMonitor.Object, NullLogger<GrpcOrderStatusProvider>.Instance);
    }

    private static OrderStatus BuildValidProto()
        => new OrderStatus
        {
            OrderId = Guid.NewGuid().ToString(),
            Status = "2",
            UserId = Guid.NewGuid().ToString(),
            SellerId = Guid.NewGuid().ToString(),
            Items =
            {
                new OrderItem
                {
                    OrderLineId = Guid.NewGuid().ToString(),
                    SkuIdStr = Guid.NewGuid().ToString(),
                    SpuId = Guid.NewGuid().ToString(),
                    Quantity = 1
                }
            }
        };

    [Fact]
    public async Task GetOrderStatusAsync_Should_Throw_When_OrderLineId_Missing()
    {
        var provider = CreateProvider(out _);
        // 反射调用私有 MapToInfo 方法验证：缺 OrderLineId 抛 AntiCorruptionException
        var proto = BuildValidProto();
        proto.Items[0] = new OrderItem
        {
            // 缺 order_line_id
            SkuIdStr = Guid.NewGuid().ToString(),
            SpuId = Guid.NewGuid().ToString(),
            Quantity = 1
        };

        var mapInfo = typeof(GrpcOrderStatusProvider)
            .GetMethod("MapToInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var ex = Assert.Throws<TargetInvocationException>(() => mapInfo.Invoke(null, new object[] { proto }));
        Assert.IsType<AntiCorruptionException>(ex.InnerException);
        Assert.Equal("ORDER_REMOTE_FAILED", ((AntiCorruptionException)ex.InnerException!).ErrorCode);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Should_Throw_When_SpuId_Empty()
    {
        var provider = CreateProvider(out _);
        var proto = BuildValidProto();
        proto.Items[0] = new OrderItem
        {
            OrderLineId = Guid.NewGuid().ToString(),
            SkuIdStr = Guid.NewGuid().ToString(),
            SpuId = Guid.Empty.ToString(),
            Quantity = 1
        };

        var mapInfo = typeof(GrpcOrderStatusProvider)
            .GetMethod("MapToInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var ex = Assert.Throws<TargetInvocationException>(() => mapInfo.Invoke(null, new object[] { proto }));
        Assert.IsType<AntiCorruptionException>(ex.InnerException);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Should_Throw_When_OrderId_Empty()
    {
        var provider = CreateProvider(out _);
        var proto = BuildValidProto();
        proto.OrderId = Guid.Empty.ToString();

        var mapInfo = typeof(GrpcOrderStatusProvider)
            .GetMethod("MapToInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var ex = Assert.Throws<TargetInvocationException>(() => mapInfo.Invoke(null, new object[] { proto }));
        Assert.IsType<AntiCorruptionException>(ex.InnerException);
    }

    [Fact]
    public async Task GetOrderStatusAsync_Should_Throw_When_SellerId_Missing()
    {
        var provider = CreateProvider(out _);
        var proto = BuildValidProto();
        // 清空 SellerId（HasSellerId=false）
        proto.ClearSellerId();

        var mapInfo = typeof(GrpcOrderStatusProvider)
            .GetMethod("MapToInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var ex = Assert.Throws<TargetInvocationException>(() => mapInfo.Invoke(null, new object[] { proto }));
        Assert.IsType<AntiCorruptionException>(ex.InnerException);
    }

    [Fact]
    public void MapToInfo_Should_Return_Complete_Info_When_All_Fields_Valid()
    {
        var provider = CreateProvider(out _);
        var expectedOrderId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedSellerId = Guid.NewGuid();
        var expectedLineId = Guid.NewGuid();
        var expectedSkuId = Guid.NewGuid();
        var expectedSpuId = Guid.NewGuid();
        var proto = new OrderStatus
        {
            OrderId = expectedOrderId.ToString(),
            Status = "2",
            UserId = expectedUserId.ToString(),
            SellerId = expectedSellerId.ToString(),
            Items =
            {
                new OrderItem
                {
                    OrderLineId = expectedLineId.ToString(),
                    SkuIdStr = expectedSkuId.ToString(),
                    SpuId = expectedSpuId.ToString(),
                    Quantity = 2
                }
            }
        };

        var mapInfo = typeof(GrpcOrderStatusProvider)
            .GetMethod("MapToInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var info = (Leno.ReviewAfterSales.Domain.Services.OrderStatusInfo)mapInfo.Invoke(null, new object[] { proto })!;

        Assert.Equal(expectedOrderId, info.OrderId);
        Assert.Equal(expectedUserId, info.UserId);
        Assert.Equal(expectedSellerId, info.SellerId);
        Assert.Single(info.Items);
        Assert.Equal(expectedLineId, info.Items[0].OrderLineId);
        Assert.Equal(expectedSkuId, info.Items[0].SkuId);
        Assert.Equal(expectedSpuId, info.Items[0].SpuId);
        Assert.Equal(2, info.Items[0].Quantity);
    }
}
