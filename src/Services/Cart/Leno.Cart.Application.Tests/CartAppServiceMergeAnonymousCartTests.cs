using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// CartAppService.MergeAnonymousCartAsync 幂等合并测试（P1-1）。
/// 验证：
/// 1. 首次合并走完整流程：ExistsAsync=false → MergeFrom → AddAsync 合并记录 → SaveEntitiesAsync → RemoveAsync
/// 2. 已合并（ExistsAsync=true）跳过 MergeFrom，不调用 AddAsync/RemoveAsync，直接返回用户购物车
/// 3. Redis RemoveAsync 失败时不回滚事务（合并记录已入库），仍返回购物车
/// 4. 匿名购物车不存在时跳过合并
/// </summary>
public class CartAppServiceMergeAnonymousCartTests
{
    private readonly Mock<ICartRepository> _cartRepoMock = new();
    private readonly Mock<ICartPriceService> _priceServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IAnonymousCartRepository> _anonymousCartRepoMock = new();
    private readonly Mock<ICartMergeRecordRepository> _cartMergeRecordRepoMock = new();
    private readonly Mock<ILogger<CartAppService>> _loggerMock = new();
    private readonly CartAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private const string AnonymousId = "anon-session-1";

    public CartAppServiceMergeAnonymousCartTests()
    {
        _sut = new CartAppService(
            _cartRepoMock.Object,
            _priceServiceMock.Object,
            _uowMock.Object,
            _anonymousCartRepoMock.Object,
            _cartMergeRecordRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task MergeAnonymousCartAsync_FirstMerge_ShouldExecuteFullFlowAndInsertMergeRecord()
    {
        // Arrange：首次合并，ExistsAsync=false
        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        anonymousCart.AddItem(SkuId, 2, SellerId);
        var userCart = CartAggregate.Create(Guid.NewGuid(), UserId);

        _cartMergeRecordRepoMock.Setup(r => r.ExistsAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _anonymousCartRepoMock.Setup(r => r.GetAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(anonymousCart);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);
        _anonymousCartRepoMock.Setup(r => r.RemoveAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        SetupPriceService();

        // Act
        var result = await _sut.MergeAnonymousCartAsync(UserId, AnonymousId);

        // Assert：完整流程执行
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].SkuId.Should().Be(SkuId);
        result.Items[0].Quantity.Should().Be(2);

        // 验证调用顺序与次数
        _cartMergeRecordRepoMock.Verify(r => r.ExistsAsync(AnonymousId, It.IsAny<CancellationToken>()), Times.Once);
        _anonymousCartRepoMock.Verify(r => r.GetAsync(AnonymousId, It.IsAny<CancellationToken>()), Times.Once);
        _cartMergeRecordRepoMock.Verify(r => r.AddAsync(It.Is<CartMergeRecord>(rec =>
            rec.AnonymousId == AnonymousId && rec.UserId == UserId && rec.MergedCount == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _anonymousCartRepoMock.Verify(r => r.RemoveAsync(AnonymousId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MergeAnonymousCartAsync_AlreadyMerged_ShouldSkipMergeFromAndReturnExistingCart()
    {
        // Arrange：合并记录已存在，应跳过 MergeFrom
        var userCart = CartAggregate.Create(Guid.NewGuid(), UserId);
        userCart.AddItem(SkuId, 1, SellerId); // 用户购物车已有 1 项

        _cartMergeRecordRepoMock.Setup(r => r.ExistsAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);
        SetupPriceService();

        // Act
        var result = await _sut.MergeAnonymousCartAsync(UserId, AnonymousId);

        // Assert：跳过 MergeFrom，用户购物车项数量不变（仍为 1，未翻倍）
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Quantity.Should().Be(1);

        // 关键断言：不加载匿名购物车、不调用 AddAsync、不调用 RemoveAsync、不调用 SaveEntitiesAsync
        _cartMergeRecordRepoMock.Verify(r => r.ExistsAsync(AnonymousId, It.IsAny<CancellationToken>()), Times.Once);
        _anonymousCartRepoMock.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cartMergeRecordRepoMock.Verify(r => r.AddAsync(It.IsAny<CartMergeRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _anonymousCartRepoMock.Verify(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MergeAnonymousCartAsync_RedisRemoveAsyncFails_ShouldNotRollbackAndReturnCart()
    {
        // Arrange：合并记录已入库后，Redis RemoveAsync 抛异常，事务不应回滚
        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        anonymousCart.AddItem(SkuId, 3, SellerId);
        var userCart = CartAggregate.Create(Guid.NewGuid(), UserId);

        _cartMergeRecordRepoMock.Setup(r => r.ExistsAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _anonymousCartRepoMock.Setup(r => r.GetAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(anonymousCart);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);
        // RemoveAsync 抛异常模拟 Redis 故障
        _anonymousCartRepoMock.Setup(r => r.RemoveAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));
        SetupPriceService();

        // Act：不应抛出（RemoveAsync 失败仅记录日志）
        var result = await _sut.MergeAnonymousCartAsync(UserId, AnonymousId);

        // Assert：事务已提交，合并记录已入库，购物车返回正常
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Quantity.Should().Be(3);

        // 关键断言：SaveEntitiesAsync 在 RemoveAsync 之前已调用（事务已提交）
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cartMergeRecordRepoMock.Verify(r => r.AddAsync(It.IsAny<CartMergeRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        _anonymousCartRepoMock.Verify(r => r.RemoveAsync(AnonymousId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MergeAnonymousCartAsync_AnonymousCartNotFound_ShouldSkipMergeAndReturnExistingCart()
    {
        // Arrange：匿名购物车不存在（已过期或从未创建）
        _cartMergeRecordRepoMock.Setup(r => r.ExistsAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _anonymousCartRepoMock.Setup(r => r.GetAsync(AnonymousId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);
        SetupPriceService();

        // Act
        var result = await _sut.MergeAnonymousCartAsync(UserId, AnonymousId);

        // Assert：返回空用户购物车（GetOrCreateCartAsync 创建空购物车）
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();

        // 不插入合并记录、不调用 RemoveAsync、不调用 SaveEntitiesAsync
        _cartMergeRecordRepoMock.Verify(r => r.AddAsync(It.IsAny<CartMergeRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _anonymousCartRepoMock.Verify(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MergeAnonymousCartAsync_EmptyUserId_ShouldThrowCartDomainException()
    {
        var act = () => _sut.MergeAnonymousCartAsync(Guid.Empty, AnonymousId);

        await act.Should().ThrowAsync<CartDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public async Task MergeAnonymousCartAsync_EmptyAnonymousId_ShouldThrowCartDomainException()
    {
        var act = () => _sut.MergeAnonymousCartAsync(UserId, string.Empty);

        await act.Should().ThrowAsync<CartDomainException>().WithMessage("*匿名会话标识*");
    }

    private void SetupPriceService()
    {
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Select(id => new SkuPriceSnapshot
                {
                    SkuId = id,
                    Price = 19.9m,
                    Currency = "CNY",
                    Available = true,
                    Title = "在售商品",
                    MainImageUrl = "https://img.example.com/a.jpg",
                    SellerId = SellerId
                }).ToList());
    }
}
