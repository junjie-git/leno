using System.Reflection;
using Leno.Infrastructure.AntiCorruption;
using Leno.Review.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Enums;
using Leno.SharedContracts.Grpc.Order.V1;
using Xunit;

using OrderStatusInfo = Leno.Review.Domain.Services.OrderStatusInfo;

namespace Leno.Review.Api.Tests.Unit;

/// <summary>
/// 审计 3.5：GrpcOrderStatusProvider 返回 OrderLineId=Guid.Empty 且 SkuId 可能丢失。
/// 验证 proto 缺关键字段时抛 AntiCorruptionException 而非静默 Guid.Empty；
/// 并固化当前契约事实：order.proto 的 order_line_id/spu_id 尚未随 Generated/Order.cs 重新生成，
/// 因此订单行映射的 OrderLineId/SpuId 暂为 Guid.Empty（待契约重新生成后需同步恢复断言）。
/// </summary>
public sealed class GrpcOrderStatusProviderMappingTests
{
    private static readonly MethodInfo MapToInfoMethod =
        typeof(GrpcOrderStatusProvider).GetMethod(
            "MapToInfo", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("未找到 GrpcOrderStatusProvider.MapToInfo");

    private static OrderStatusInfo InvokeMapToInfo(OrderStatus proto)
    {
        var result = MapToInfoMethod.Invoke(null, new object[] { proto });
        return (OrderStatusInfo)(result ?? throw new InvalidOperationException("MapToInfo 返回 null"));
    }

    private static OrderStatus BuildValidProto()
        => new()
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
    public void MapToInfo_Should_Throw_When_OrderId_Empty()
    {
        var proto = BuildValidProto();
        proto.OrderId = Guid.Empty.ToString();

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeMapToInfo(proto));
        var inner = Assert.IsType<AntiCorruptionException>(exception.InnerException);
        Assert.Equal("ORDER_REMOTE_FAILED", inner.ErrorCode);
    }

    [Fact]
    public void MapToInfo_Should_Throw_When_UserId_Missing()
    {
        var proto = BuildValidProto();
        proto.ClearUserId();

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeMapToInfo(proto));
        var inner = Assert.IsType<AntiCorruptionException>(exception.InnerException);
        Assert.Equal("ORDER_REMOTE_FAILED", inner.ErrorCode);
    }

    [Fact]
    public void MapToInfo_Should_Throw_When_SellerId_Missing()
    {
        var proto = BuildValidProto();
        proto.ClearSellerId();

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeMapToInfo(proto));
        var inner = Assert.IsType<AntiCorruptionException>(exception.InnerException);
        Assert.Equal("ORDER_REMOTE_FAILED", inner.ErrorCode);
    }

    [Fact]
    public void MapToInfo_Should_Throw_When_SkuId_Missing()
    {
        var proto = BuildValidProto();
        proto.Items[0] = new OrderItem
        {
            OrderLineId = Guid.NewGuid().ToString(),
            SpuId = Guid.NewGuid().ToString(),
            Quantity = 1
        };

        var exception = Assert.Throws<TargetInvocationException>(() => InvokeMapToInfo(proto));
        var inner = Assert.IsType<AntiCorruptionException>(exception.InnerException);
        Assert.Equal("ORDER_REMOTE_FAILED", inner.ErrorCode);
    }

    [Fact]
    public void MapToInfo_Should_Map_Valid_Fields_And_Document_Pending_Contract_Gap()
    {
        var expectedOrderId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedSellerId = Guid.NewGuid();
        var expectedSkuId = Guid.NewGuid();
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
                    OrderLineId = Guid.NewGuid().ToString(),
                    SkuIdStr = expectedSkuId.ToString(),
                    SpuId = Guid.NewGuid().ToString(),
                    Quantity = 2
                }
            }
        };

        var info = InvokeMapToInfo(proto);

        Assert.Equal(expectedOrderId, info.OrderId);
        Assert.Equal(expectedUserId, info.UserId);
        Assert.Equal(expectedSellerId, info.SellerId);
        Assert.Equal((int)OrderStatusEnum.Shipped, info.Status);
        Assert.Single(info.Items);
        Assert.Equal(expectedSkuId, info.Items[0].SkuId);
        Assert.Equal(expectedSellerId, info.Items[0].SellerId);
        Assert.Equal(2, info.Items[0].Quantity);
        // 契约缺口：order.proto 已定义 order_line_id/spu_id，但 Generated/Order.cs 未重新生成，
        // 当前映射只能置 Guid.Empty；重新生成后应改为断言真实值。
        Assert.Equal(Guid.Empty, info.Items[0].OrderLineId);
        Assert.Equal(Guid.Empty, info.Items[0].SpuId);
    }
}
