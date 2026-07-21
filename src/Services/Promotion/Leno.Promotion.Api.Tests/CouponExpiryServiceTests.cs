using Leno.Promotion.Api.BackgroundServices;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Promotion.Api.Tests;

/// <summary>
/// P0-2.1 测试：覆盖 CouponExpiryService 分页扫描在状态变更后不漏处理的行为。
/// </summary>
public class CouponExpiryServiceTests
{
    private readonly Mock<IUserCouponRepository> _userCouponRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task ProcessExpiredCouponsAsync_LargeBatch_ShouldNotSkipRecords()
    {
        // 模拟 700 张过期券，分两批返回（每批 500 + 200）
        // 关键场景：第一批 Expire 后状态由 Unused→Expired，下次查询时这批已不在结果集，
        // 原 skip += BatchSize 实现会跳过当前结果集前 500 条（即原 501-700 号记录），导致 200 张漏处理
        var callCount = 0;
        var allCoupons = Enumerable.Range(0, 700).Select(_ => CreateUserCoupon()).ToList();

        _userCouponRepoMock.Setup(r => r.GetExpiredUnusedCouponsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // 第一批：返回前 500 张
                    return allCoupons.Take(500).ToList();
                }
                if (callCount == 2)
                {
                    // 第二批：skip=0 时返回剩余 200 张（依赖状态过滤淘汰已 Expire 的记录）
                    return allCoupons.Skip(500).Take(200).ToList();
                }
                return new List<UserCoupon>();
            });
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await InvokeProcessExpiredCouponsAsync();

        // 关键断言：每次查询 skip 始终为 0（依赖状态过滤淘汰已处理记录），不会漏处理
        _userCouponRepoMock.Verify(
            r => r.GetExpiredUnusedCouponsAsync(0, 500, It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
        _userCouponRepoMock.Verify(
            r => r.GetExpiredUnusedCouponsAsync(It.Is<int>(s => s > 0), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task InvokeProcessExpiredCouponsAsync()
    {
        var scopeFactory = new ServiceCollection()
            .AddSingleton(_userCouponRepoMock.Object)
            .AddSingleton(_unitOfWorkMock.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var svc = new CouponExpiryService(scopeFactory, new Mock<ILogger<CouponExpiryService>>().Object);
        var method = typeof(CouponExpiryService).GetMethod(
            "ProcessExpiredCouponsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(svc, new object[] { CancellationToken.None })!;
    }

    private static UserCoupon CreateUserCoupon()
        => UserCoupon.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Manual", DateTime.UtcNow.AddDays(30));
}
