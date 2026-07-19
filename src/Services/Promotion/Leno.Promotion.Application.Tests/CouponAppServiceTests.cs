using Leno.Promotion.Application.Services;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.Promotion.Application.Tests;

/// <summary>
/// CouponAppService.ReleaseCouponsAsync 用例补充测试（与 PromotionAppServiceTests.cs 中的 partial 类合并）。
/// </summary>
public partial class CouponAppServiceTests
{
    private static readonly Guid ReleaseOrderId = Guid.NewGuid();

    [Fact]
    public async Task ReleaseCoupons_NoLockedCoupons_ReturnsIdempotently()
    {
        // mock 返回空列表，模拟订单未绑定任何锁定券，应当幂等返回不调用 SaveEntitiesAsync
        _userCouponRepoMock.Setup(r => r.GetAllByLockedOrderIdAsync(ReleaseOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserCoupon>());

        await _sut.ReleaseCouponsAsync(ReleaseOrderId, CancellationToken.None);

        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _userCouponRepoMock.Verify(r => r.GetAllByLockedOrderIdAsync(ReleaseOrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseCoupons_HasLockedCoupons_CallsReleaseAndSaves()
    {
        // mock 返回 2 张 Locked 用户券，验证每张调用 Release() 后状态变为 Unused，且 SaveEntitiesAsync 调用 1 次
        var coupon1 = CreateLockedUserCoupon(ReleaseOrderId);
        var coupon2 = CreateLockedUserCoupon(ReleaseOrderId);
        _userCouponRepoMock.Setup(r => r.GetAllByLockedOrderIdAsync(ReleaseOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserCoupon> { coupon1, coupon2 });

        await _sut.ReleaseCouponsAsync(ReleaseOrderId, CancellationToken.None);

        coupon1.Status.Should().Be(CouponStatus.Unused);
        coupon1.LockedOrderId.Should().BeNull();
        coupon2.Status.Should().Be(CouponStatus.Unused);
        coupon2.LockedOrderId.Should().BeNull();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _userCouponRepoMock.Verify(r => r.GetAllByLockedOrderIdAsync(ReleaseOrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UserCoupon CreateLockedUserCoupon(Guid orderId)
    {
        // 通过 Receive 工厂创建 Unused 券，再调用 Lock(orderId) 转入 Locked 态用于测试
        var userCoupon = UserCoupon.Receive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Manual",
            DateTime.UtcNow.AddDays(10));
        userCoupon.Lock(orderId);
        return userCoupon;
    }
}
