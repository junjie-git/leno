using Grpc.Core;
using Leno.Promotion.Api.GrpcServices;
using Leno.Promotion.Application;
using Leno.SharedContracts.Grpc.Promotion.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Promotion.Api.Tests;

/// <summary>
/// PromotionGrpcService.LockCoupon / ReleaseCoupons 单元测试。
/// 验证 gRPC handler 将 proto 字段正确解析为 Guid 并调用对应 AppService 方法。
/// </summary>
public class PromotionGrpcServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CouponId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public async Task LockCoupon_ValidInput_CallsAppService()
    {
        var couponAppServiceMock = new Mock<ICouponAppService>();
        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            couponAppServiceMock.Object,
            NullLogger<PromotionGrpcService>.Instance);

        var request = new LockCouponRequest
        {
            UserId = UserId.ToString(),
            CouponId = CouponId.ToString(),
            OrderId = OrderId.ToString()
        };

        var result = await sut.LockCoupon(request, new TestServerCallContext());

        result.Success.Should().BeTrue();
        couponAppServiceMock.Verify(
            c => c.LockCouponAsync(UserId, CouponId, OrderId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LockCoupon_InvalidGuid_ThrowsInvalidArgument()
    {
        var couponAppServiceMock = new Mock<ICouponAppService>(MockBehavior.Strict);
        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            couponAppServiceMock.Object,
            NullLogger<PromotionGrpcService>.Instance);

        var request = new LockCouponRequest
        {
            UserId = "not-a-guid",
            CouponId = CouponId.ToString(),
            OrderId = OrderId.ToString()
        };

        var act = async () => await sut.LockCoupon(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        couponAppServiceMock.Verify(
            c => c.LockCouponAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LockCoupon_InvalidOrderId_ThrowsInvalidArgument()
    {
        var couponAppServiceMock = new Mock<ICouponAppService>(MockBehavior.Strict);
        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            couponAppServiceMock.Object,
            NullLogger<PromotionGrpcService>.Instance);

        var request = new LockCouponRequest
        {
            UserId = UserId.ToString(),
            CouponId = CouponId.ToString(),
            OrderId = "not-a-guid"
        };

        var act = async () => await sut.LockCoupon(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        couponAppServiceMock.Verify(
            c => c.LockCouponAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseCoupons_ValidInput_CallsAppService()
    {
        var couponAppServiceMock = new Mock<ICouponAppService>();
        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            couponAppServiceMock.Object,
            NullLogger<PromotionGrpcService>.Instance);

        var request = new ReleaseCouponsRequest
        {
            OrderId = OrderId.ToString()
        };

        var result = await sut.ReleaseCoupons(request, new TestServerCallContext());

        result.Success.Should().BeTrue();
        couponAppServiceMock.Verify(
            c => c.ReleaseCouponsAsync(OrderId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReleaseCoupons_InvalidOrderId_ThrowsInvalidArgument()
    {
        var couponAppServiceMock = new Mock<ICouponAppService>(MockBehavior.Strict);
        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            couponAppServiceMock.Object,
            NullLogger<PromotionGrpcService>.Instance);

        var request = new ReleaseCouponsRequest
        {
            OrderId = "not-a-guid"
        };

        var act = async () => await sut.ReleaseCoupons(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        couponAppServiceMock.Verify(
            c => c.ReleaseCouponsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
