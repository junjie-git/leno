using FluentAssertions;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure;
using Leno.ReviewAfterSales.Infrastructure.Repositories;
using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Leno.Testing.Fixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using AfterSalesAggregate = Leno.ReviewAfterSales.Domain.Aggregates.AfterSales;

namespace Leno.ReviewAfterSales.Infrastructure.Tests.Integration;

/// <summary>
/// 卖家越权集成测试：覆盖 AfterSalesAppService.ApproveAfterSalesAsync 经 RequireOwnedAfterSales 校验卖家归属。
/// 依赖 Plan 1 F1.4 已落地越权校验（错误码 AFTERSALES_NOT_OWNED，ReviewDomainException）。
/// </summary>
public class SellerOwnershipIntegrationTests : CrossBcIntegrationTestBase<ReviewAfterSalesDbContext>
{
    public SellerOwnershipIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<ReviewAfterSalesDbContext>(options => options.UseSqlServer(sqlConnectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAfterSalesRepository, EfCoreAfterSalesRepository>();

        // 防腐层 Mock：Approve 路径在越权校验之前不会调用，仅为构造函数注入
        services.AddScoped(_ => Mock.Of<IAfterSalesEligibilityChecker>());
        services.AddScoped(_ => Mock.Of<IPaymentInfoQueryService>());
        services.AddScoped(_ => Mock.Of<IEventBus>());

        services.AddScoped<AfterSalesAppService>();
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        // 本测试不注册消费者，仅验证应用层归属校验
    }

    [Fact]
    public async Task ApproveAfterSalesAsync_WhenOperatorIsNotOwner_ShouldThrowReviewDomainException()
    {
        // Arrange：以归属卖家 sellerId 创建售后单
        var sellerId = Guid.NewGuid();
        var afterSalesId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using (var seedScope = ServiceProvider.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ReviewAfterSalesDbContext>();
            var afterSales = CreatePendingAfterSales(afterSalesId, orderId, sellerId);
            seedDb.AfterSales.Add(afterSales);
            await seedDb.SaveChangesAsync();
        }

        // Act：以非归属卖家调用 ApproveAfterSalesAsync
        using var actScope = ServiceProvider.CreateScope();
        var appService = actScope.ServiceProvider.GetRequiredService<AfterSalesAppService>();
        var nonOwnerOperator = Guid.NewGuid();

        var act = async () => await appService.ApproveAfterSalesAsync(
            afterSalesId,
            operatorId: nonOwnerOperator,
            approvedAmount: 50m,
            CancellationToken.None);

        // Assert：抛 ReviewDomainException，错误码 AFTERSALES_NOT_OWNED
        var ex = await act.Should().ThrowAsync<ReviewDomainException>(
            "非归属卖家调用 ApproveAfterSalesAsync 应被 RequireOwnedAfterSales 拦截");
        ex.Which.ErrorCode.Should().Be("AFTERSALES_NOT_OWNED", "错误码应为 AFTERSALES_NOT_OWNED");
        ex.Which.Message.Should().Contain("无权操作此售后单");
    }

    /// <summary>
    /// 构造一个待审核售后单：AfterSales.Create 校验入参后置 Pending 态。
    /// </summary>
    private static AfterSalesAggregate CreatePendingAfterSales(Guid afterSalesId, Guid orderId, Guid sellerId)
    {
        var userId = Guid.NewGuid();

        return AfterSalesAggregate.Create(
            afterSalesId,
            orderId,
            orderLineId: null,
            userId,
            sellerId,
            type: AfterSalesType.RefundOnly,
            reasonCategory: "质量问题",
            reason: "商品存在质量瑕疵，申请仅退款",
            images: new List<string>(),
            requestedAmount: 99.9m,
            currency: "CNY");
    }
}
