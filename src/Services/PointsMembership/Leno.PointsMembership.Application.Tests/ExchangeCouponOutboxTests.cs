using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 验证 <see cref="ExchangeCouponAppService.ExchangeCouponAsync"/> 不再直接调用 <c>IEventBus.PublishAsync</c>，
/// 改为通过聚合根 <see cref="PointsAccount.RequestExchangeCoupon"/> 在同一事务内追加领域事件，经 Outbox 翻译为集成事件。
/// 关联审计 PM-H05。
/// </summary>
public sealed class ExchangeCouponOutboxTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid CouponTemplateId = Guid.NewGuid();

    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ExchangeCouponAppService _service;

    public ExchangeCouponOutboxTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _service = new ExchangeCouponAppService(
            _accountRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<ExchangeCouponAppService>.Instance);
    }

    [Fact]
    public async Task ExchangeCouponAsync_Should_Raise_DomainEvent_Instead_Of_Calling_EventBus()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 500, "种子积分");
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var dto = new ExchangeCouponDto
        {
            UserId = UserId,
            CouponTemplateId = CouponTemplateId,
            PointsRequired = 100
        };

        var result = await _service.ExchangeCouponAsync(dto, CancellationToken.None);

        // 应在聚合根上添加领域事件（经 Outbox 翻译为集成事件）
        var domainEvent = account.DomainEvents.OfType<PointsExchangeCouponRequestedDomainEvent>().SingleOrDefault();
        domainEvent.Should().NotBeNull();
        domainEvent!.UserId.Should().Be(UserId);
        domainEvent.CouponTemplateId.Should().Be(CouponTemplateId);
        domainEvent.PointsRequired.Should().Be(100);
        domainEvent.ExchangeId.Should().Be(result.ExchangeId);

        // 应同事务冻结积分
        account.FrozenBalance.Should().Be(100);
        account.Balance.Should().Be(400);

        // 应有一次 SaveEntitiesAsync 提交
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExchangeCouponAsync_Should_Throw_When_Account_NotFound()
    {
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var dto = new ExchangeCouponDto
        {
            UserId = UserId,
            CouponTemplateId = CouponTemplateId,
            PointsRequired = 100
        };

        var act = () => _service.ExchangeCouponAsync(dto, CancellationToken.None);

        await act.Should().ThrowAsync<Domain.Exceptions.PointsDomainException>()
            .WithMessage("*不存在*");
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeCouponAsync_Should_Throw_When_Balance_Insufficient()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 50, "种子积分");
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var dto = new ExchangeCouponDto
        {
            UserId = UserId,
            CouponTemplateId = CouponTemplateId,
            PointsRequired = 100
        };

        var act = () => _service.ExchangeCouponAsync(dto, CancellationToken.None);

        await act.Should().ThrowAsync<Domain.Exceptions.PointsDomainException>()
            .WithMessage("*余额不足*");
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
