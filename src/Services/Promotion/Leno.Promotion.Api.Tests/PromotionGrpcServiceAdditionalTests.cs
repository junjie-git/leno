using Grpc.Core;
using Leno.Promotion.Api.GrpcServices;
using Leno.Promotion.Application;
using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedContracts.Grpc.Promotion.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Promotion.Api.Tests;

/// <summary>
/// PromotionGrpcService.GetCouponInfo 补充单元测试（P0-2.11）。
/// 验证 gRPC handler 通过 ICouponAppService.GetByIdAsync 查询券信息，
/// 不再直接依赖 ICouponRepository 领域仓储。
/// </summary>
public class PromotionGrpcServiceAdditionalTests
{
    [Fact]
    public async Task GetCouponInfo_ValidCouponId_ShouldCallAppServiceNotRepository()
    {
        // 修复后 gRPC 服务应仅调用 ICouponAppService.GetByIdAsync，不再注入 ICouponRepository
        var couponId = Guid.NewGuid();
        var couponDto = new CouponDto
        {
            Id = couponId,
            Name = "Test",
            Type = CouponType.FixedAmount,
            FaceValue = 20m,
            MinSpend = 100m,
            ValidityType = CouponValidityType.RelativeDays,
            TotalQty = 1000,
            IssuedQty = 0,
            Status = CouponTemplateStatus.Enabled,
            CreatedAt = DateTime.UtcNow
        };

        var calculateServiceMock = new Mock<IPromotionCalculateAppService>();
        var couponAppServiceMock = new Mock<ICouponAppService>();
        couponAppServiceMock
            .Setup(s => s.GetByIdAsync(couponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(couponDto);

        var sut = new PromotionGrpcService(
            calculateServiceMock.Object,
            couponAppServiceMock.Object,
            NullLogger<PromotionGrpcService>.Instance);

        var request = new GetCouponInfoRequest { CouponId = couponId.ToString() };
        var result = await sut.GetCouponInfo(request, new TestServerCallContext());

        result.CouponId.Should().Be(couponId.ToString());
        result.Title.Should().Be("Test");
        result.DiscountCents.Should().Be(2000);
        result.Status.Should().Be("Enabled");
        couponAppServiceMock.Verify(
            s => s.GetByIdAsync(couponId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCouponInfo_NotFound_ShouldThrowNotFound()
    {
        var couponId = Guid.NewGuid();
        var couponAppServiceMock = new Mock<ICouponAppService>();
        couponAppServiceMock
            .Setup(s => s.GetByIdAsync(couponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CouponDto?)null);

        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            couponAppServiceMock.Object,
            NullLogger<PromotionGrpcService>.Instance);

        var request = new GetCouponInfoRequest { CouponId = couponId.ToString() };
        var act = async () => await sut.GetCouponInfo(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.NotFound);
        couponAppServiceMock.Verify(
            s => s.GetByIdAsync(couponId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCouponInfo_InvalidGuid_ShouldThrowInvalidArgument()
    {
        var couponAppServiceMock = new Mock<ICouponAppService>(MockBehavior.Strict);

        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            couponAppServiceMock.Object,
            NullLogger<PromotionGrpcService>.Instance);

        var request = new GetCouponInfoRequest { CouponId = "not-a-guid" };
        var act = async () => await sut.GetCouponInfo(request, new TestServerCallContext());

        var ex = (await act.Should().ThrowAsync<RpcException>()).Which;
        ex.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
        couponAppServiceMock.Verify(
            s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
