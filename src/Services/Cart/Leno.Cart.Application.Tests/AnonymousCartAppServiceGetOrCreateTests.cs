using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// AnonymousCartAppService.GetOrCreateCartAsync SET NX 原子创建测试（P2-10）。
/// 验证：并发场景下使用 TrySaveAsync（Redis SET NX）原子创建，避免两个请求同时遇 null 都创建并覆盖后者丢失。
/// 测试通过 GetCartAsync 触发 GetOrCreateCartAsync（private 方法，经公共 API 验证）。
/// </summary>
public class AnonymousCartAppServiceGetOrCreateTests
{
    private readonly Mock<IAnonymousCartRepository> _repoMock = new();
    private readonly Mock<ICartPriceService> _priceMock = new();
    private readonly AnonymousCartAppService _sut;

    private const string SessionId = "session-p2-10";

    public AnonymousCartAppServiceGetOrCreateTests()
    {
        _sut = new AnonymousCartAppService(_repoMock.Object, _priceMock.Object);
    }

    [Fact]
    public async Task GetCartAsync_CartExists_ShouldReturnExistingWithoutTrySave()
    {
        // Arrange：购物车已存在，不应调用 TrySaveAsync
        var existingCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        var skuId = Guid.NewGuid();
        existingCart.AddItem(skuId, 2, Guid.NewGuid());
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCart);
        SetupPriceService();

        // Act
        var result = await _sut.GetCartAsync(SessionId);

        // Assert：返回已存在的购物车，不调用 TrySaveAsync
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].SkuId.Should().Be(skuId);
        _repoMock.Verify(r => r.TrySaveAsync(It.IsAny<string>(), It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCartAsync_CartNotExists_TrySaveSucceeds_ShouldReturnNewCartWithoutReread()
    {
        // Arrange：购物车不存在，TrySaveAsync 返回 true（成功创建）
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);
        _repoMock.Setup(r => r.TrySaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        SetupPriceService();

        // Act
        var result = await _sut.GetCartAsync(SessionId);

        // Assert：返回新创建的空购物车
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();

        // 关键断言：GetAsync 仅调用 1 次（不重新读取，因为 TrySave 返回 true）
        _repoMock.Verify(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.TrySaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        // 不调用 SaveAsync（旧的非原子创建路径已被替代）
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCartAsync_CartNotExists_TrySaveFails_ShouldReReadExistingCart()
    {
        // Arrange：并发场景——两个请求同时遇 null，对方先写入，本次 TrySave 返回 false
        var concurrentCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        var skuId = Guid.NewGuid();
        concurrentCart.AddItem(skuId, 3, Guid.NewGuid());

        _repoMock.SetupSequence(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null)   // 第一次 GetAsync：null
            .ReturnsAsync(concurrentCart);         // 第二次 GetAsync（TrySave 失败后重读）：返回并发创建的购物车
        _repoMock.Setup(r => r.TrySaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // 并发请求已创建，SET NX 失败
        SetupPriceService();

        // Act
        var result = await _sut.GetCartAsync(SessionId);

        // Assert：返回并发请求创建的购物车（而非本次创建的空购物车）
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].SkuId.Should().Be(skuId);
        result.Items[0].Quantity.Should().Be(3);

        // 关键断言：GetAsync 调用 2 次（初次 null + TrySave 失败后重读）
        _repoMock.Verify(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _repoMock.Verify(r => r.TrySaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCartAsync_CartNotExists_TrySaveFailsAndReReadNull_ShouldFallbackToNewCart()
    {
        // Arrange：极端情况——TrySave 返回 false（并发已写入），但重读时已被并发删除返回 null
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null); // 所有 GetAsync 都返回 null
        _repoMock.Setup(r => r.TrySaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // 并发请求已创建（但随后被删除）
        SetupPriceService();

        // Act
        var result = await _sut.GetCartAsync(SessionId);

        // Assert：回退使用本次创建的空购物车（无业务损失，空购物车）
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();

        // GetAsync 调用 2 次（初次 null + TrySave 失败后重读仍 null）
        _repoMock.Verify(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _repoMock.Verify(r => r.TrySaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_CartNotExists_ShouldUseTrySaveForAtomicCreate()
    {
        // Arrange：AddItemAsync 也通过 GetOrCreateCartAsync 创建购物车，应使用 TrySaveAsync
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);
        _repoMock.Setup(r => r.TrySaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.SaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        SetupPriceService();

        // Act
        var skuId = Guid.NewGuid();
        var result = await _sut.AddItemAsync(SessionId, new AddCartItemDto { SkuId = skuId, Quantity = 1, SellerId = Guid.NewGuid() });

        // Assert：购物车原子创建后添加商品
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].SkuId.Should().Be(skuId);

        // 关键断言：GetOrCreateCartAsync 使用 TrySaveAsync（非 SaveAsync）创建购物车
        _repoMock.Verify(r => r.TrySaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        // SaveAsync 仅在 AddItem 后保存变更时调用 1 次（不是创建时）
        _repoMock.Verify(r => r.SaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupPriceService()
    {
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Select(id => new SkuPriceSnapshot
                {
                    SkuId = id,
                    Price = 19.9m,
                    Currency = "CNY",
                    Available = true,
                    Title = "在售商品",
                    MainImageUrl = "https://img.example.com/a.jpg",
                    SellerId = Guid.NewGuid()
                }).ToList());
    }
}
