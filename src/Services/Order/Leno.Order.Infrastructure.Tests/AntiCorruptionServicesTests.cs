using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Auth;
using Leno.Order.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// 防腐层显式异常测试：覆盖积分域（T10）与促销域（T11）远程失败、非 2xx、超时场景。
/// 远程失败须抛 <see cref="AntiCorruptionException"/>，用户取消（CancellationToken）须透传 <see cref="OperationCanceledException"/>。
/// </summary>
public class AntiCorruptionServicesTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    // ---- PointsAntiCorruptionService (T10) ----

    [Theory]
    [InlineData("Freeze")]
    [InlineData("Confirm")]
    [InlineData("Release")]
    public async Task Points_RemoteFailure_ShouldThrowAntiCorruptionException(string operation)
    {
        var service = CreatePointsService(_ => throw new HttpRequestException("connection refused"));

        var act = () => operation switch
        {
            "Freeze" => service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None),
            "Confirm" => service.ConfirmDeductionAsync(OrderId, CancellationToken.None),
            "Release" => service.ReleaseAsync(OrderId, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        var ex = await act.Should().ThrowAsync<AntiCorruptionException>();
        ex.Which.ErrorCode.Should().NotBe("ANTICORRUPTION_ERROR");
    }

    [Theory]
    [InlineData("Freeze")]
    [InlineData("Confirm")]
    [InlineData("Release")]
    public async Task Points_NonSuccessStatusCode_ShouldThrowAntiCorruptionException(string operation)
    {
        var service = CreatePointsService(_ => Response(HttpStatusCode.InternalServerError));

        var act = () => operation switch
        {
            "Freeze" => service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None),
            "Confirm" => service.ConfirmDeductionAsync(OrderId, CancellationToken.None),
            "Release" => service.ReleaseAsync(OrderId, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Theory]
    [InlineData("Freeze")]
    [InlineData("Confirm")]
    [InlineData("Release")]
    public async Task Points_Timeout_ShouldThrowAntiCorruptionException(string operation)
    {
        // HttpClient 超时表现为 TaskCanceledException（无 CancellationToken 取消请求）
        var service = CreatePointsService(_ => throw new TaskCanceledException("timeout"));

        var act = () => operation switch
        {
            "Freeze" => service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None),
            "Confirm" => service.ConfirmDeductionAsync(OrderId, CancellationToken.None),
            "Release" => service.ReleaseAsync(OrderId, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Theory]
    [InlineData("Freeze")]
    [InlineData("Confirm")]
    [InlineData("Release")]
    public async Task Points_UserCancellation_ShouldPropagateOperationCanceledException(string operation)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = CreatePointsService(_ => throw new OperationCanceledException(cts.Token));

        var act = () => operation switch
        {
            "Freeze" => service.FreezeAsync(UserId, OrderId, 100, cts.Token),
            "Confirm" => service.ConfirmDeductionAsync(OrderId, cts.Token),
            "Release" => service.ReleaseAsync(OrderId, cts.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("Freeze")]
    [InlineData("Confirm")]
    [InlineData("Release")]
    public async Task Points_Success_ShouldNotThrow(string operation)
    {
        var service = CreatePointsService(_ => Response(HttpStatusCode.OK));

        var act = () => operation switch
        {
            "Freeze" => service.FreezeAsync(UserId, OrderId, 100, CancellationToken.None),
            "Confirm" => service.ConfirmDeductionAsync(OrderId, CancellationToken.None),
            "Release" => service.ReleaseAsync(OrderId, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        await act.Should().NotThrowAsync();
    }

    // ---- PromotionAntiCorruptionService (T11) ----

    [Fact]
    public async Task Promotion_CalculateDiscount_RemoteFailure_ShouldThrowAntiCorruptionException()
    {
        var service = CreatePromotionService(_ => throw new HttpRequestException("network down"));

        var act = () => service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 10m) }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AntiCorruptionException>();
        ex.Which.ErrorCode.Should().NotBe("ANTICORRUPTION_ERROR");
    }

    [Fact]
    public async Task Promotion_CalculateDiscount_NonSuccessStatusCode_ShouldThrowAntiCorruptionException()
    {
        var service = CreatePromotionService(_ => Response(HttpStatusCode.BadRequest));

        var act = () => service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 10m) }, CancellationToken.None);

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task Promotion_CalculateDiscount_Timeout_ShouldThrowAntiCorruptionException()
    {
        var service = CreatePromotionService(_ => throw new TaskCanceledException("timeout"));

        var act = () => service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 10m) }, CancellationToken.None);

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task Promotion_CalculateDiscount_UserCancellation_ShouldPropagateOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = CreatePromotionService(_ => throw new OperationCanceledException(cts.Token));

        var act = () => service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 10m) }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Promotion_ReleaseCoupons_RemoteFailure_ShouldThrowAntiCorruptionException()
    {
        var service = CreatePromotionService(_ => throw new HttpRequestException("network down"));

        var act = () => service.ReleaseCouponsAsync(OrderId, CancellationToken.None);

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task Promotion_ReleaseCoupons_NonSuccessStatusCode_ShouldThrowAntiCorruptionException()
    {
        var service = CreatePromotionService(_ => Response(HttpStatusCode.InternalServerError));

        var act = () => service.ReleaseCouponsAsync(OrderId, CancellationToken.None);

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task Promotion_ReleaseCoupons_Timeout_ShouldThrowAntiCorruptionException()
    {
        var service = CreatePromotionService(_ => throw new TaskCanceledException("timeout"));

        var act = () => service.ReleaseCouponsAsync(OrderId, CancellationToken.None);

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task Promotion_ReleaseCoupons_UserCancellation_ShouldPropagateOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = CreatePromotionService(_ => throw new OperationCanceledException(cts.Token));

        var act = () => service.ReleaseCouponsAsync(OrderId, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Promotion_CalculateDiscount_Success_ShouldReturnDiscount()
    {
        var service = CreatePromotionService(_ => Json(new { data = new { totalDiscountAmount = 15.5m, currency = "CNY" } }));

        var result = await service.CalculateDiscountAsync(UserId, new List<(Guid, decimal)> { (Guid.NewGuid(), 100m) }, CancellationToken.None);

        result.Should().Be(15.5m);
    }

    [Fact]
    public async Task Promotion_ReleaseCoupons_Success_ShouldNotThrow()
    {
        var service = CreatePromotionService(_ => Response(HttpStatusCode.OK));

        var act = () => service.ReleaseCouponsAsync(OrderId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ---- PromotionAntiCorruptionService.LockCoupon (Task 3) ----

    [Fact]
    public async Task Promotion_LockCoupon_RemoteFailure_ShouldThrowAntiCorruptionException()
    {
        var service = CreatePromotionService(_ => throw new HttpRequestException("network down"));

        var act = () => service.LockCouponAsync(UserId, Guid.NewGuid(), OrderId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AntiCorruptionException>();
        ex.Which.ErrorCode.Should().Be("PROMOTION_UNAVAILABLE");
    }

    [Fact]
    public async Task Promotion_LockCoupon_NonSuccessStatusCode_ShouldThrowAntiCorruptionException()
    {
        // 409 Conflict 对应促销域券已被并发订单占用
        var service = CreatePromotionService(_ => Response(HttpStatusCode.Conflict));

        var act = () => service.LockCouponAsync(UserId, Guid.NewGuid(), OrderId, CancellationToken.None);

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task Promotion_LockCoupon_Timeout_ShouldThrowAntiCorruptionException()
    {
        var service = CreatePromotionService(_ => throw new TaskCanceledException("timeout"));

        var act = () => service.LockCouponAsync(UserId, Guid.NewGuid(), OrderId, CancellationToken.None);

        await act.Should().ThrowAsync<AntiCorruptionException>();
    }

    [Fact]
    public async Task Promotion_LockCoupon_UserCancellation_ShouldPropagateOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = CreatePromotionService(_ => throw new OperationCanceledException(cts.Token));

        var act = () => service.LockCouponAsync(UserId, Guid.NewGuid(), OrderId, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Promotion_LockCoupon_Success_ShouldNotThrow()
    {
        var service = CreatePromotionService(_ => Response(HttpStatusCode.OK));

        var act = () => service.LockCouponAsync(UserId, Guid.NewGuid(), OrderId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ---- Helpers ----

    private static PointsAntiCorruptionService CreatePointsService(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var http = new HttpClient(new FakeHandler(handler)) { BaseAddress = new Uri("http://test/") };
        var options = Options.Create(new InternalApiKeyOptions());
        var logger = new Mock<ILogger<PointsAntiCorruptionService>>().Object;
        return new PointsAntiCorruptionService(http, options, logger);
    }

    private static PromotionAntiCorruptionService CreatePromotionService(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var http = new HttpClient(new FakeHandler(handler)) { BaseAddress = new Uri("http://test/") };
        var options = Options.Create(new InternalApiKeyOptions());
        var logger = new Mock<ILogger<PromotionAntiCorruptionService>>().Object;
        return new PromotionAntiCorruptionService(http, options, logger);
    }

    private static Task<HttpResponseMessage> Response(HttpStatusCode code) => Task.FromResult<HttpResponseMessage>(new HttpResponseMessage(code));

    private static Task<HttpResponseMessage> Json(object payload)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        return Task.FromResult(response);
    }

    /// <summary>
    /// 简单 HttpMessageHandler 桩，按委托返回响应或抛异常，模拟远程失败/超时/取消。
    /// </summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }
}
