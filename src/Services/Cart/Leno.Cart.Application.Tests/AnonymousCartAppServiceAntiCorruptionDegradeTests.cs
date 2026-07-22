using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Infrastructure.AntiCorruption;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// AnonymousCartAppService.BuildCartDtoAsync 价格服务故障降级测试（P1-15，P0-4 协同子项）。
/// 验证防腐层实际抛出 AntiCorruptionException（继承 DomainException）时，BuildCartDtoAsync 能命中
/// catch (DomainException ex) 降级分支：不向控制器冒泡异常、标记 PriceUnavailable=true，
/// 选中项不计入可结算金额，避免误导性 0 元可结算。
/// </summary>
public class AnonymousCartAppServiceAntiCorruptionDegradeTests
{
    private readonly Mock<IAnonymousCartRepository> _repoMock = new();
    private readonly Mock<ICartPriceService> _priceMock = new();
    private readonly AnonymousCartAppService _sut;

    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private const string SessionId = "session-anti-corruption";

    public AnonymousCartAppServiceAntiCorruptionDegradeTests()
    {
        _sut = new AnonymousCartAppService(_repoMock.Object, _priceMock.Object);
    }

    [Fact]
    public async Task GetCartAsync_WhenPriceServiceThrowsAntiCorruptionException_ShouldDegrade()
    {
        // Arrange：购物车已存在（避免走 GetOrCreateCartAsync 的 TrySaveAsync 创建路径）
        // 价格服务抛 AntiCorruptionException（P0-4 协同：防腐层实际异常类型）
        var cart = CreateAnonymousCartWithItemSelected();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AntiCorruptionException("product 防腐层网络故障", "PRODUCT_UNAVAILABLE"));

        // Act：BuildCartDtoAsync 应命中 catch (DomainException ex) 降级分支，不冒泡
        var result = await _sut.GetCartAsync(SessionId);

        // Assert：进入降级分支，标记 PriceUnavailable=true，不向控制器冒泡
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].PriceUnavailable.Should().BeTrue();
        result.Items[0].Available.Should().BeFalse();
        result.Items[0].UnitPrice.Should().Be(0m);
        result.Items[0].Title.Should().Be("[价格加载失败]");
        result.Items[0].MainImageUrl.Should().BeEmpty();
        // 选中项缺价不应计入可结算金额，避免 0 元结算单
        result.SelectedTotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task AddItemAsync_WhenPriceServiceThrowsAntiCorruptionException_ShouldDegrade()
    {
        // Arrange：写操作路径同样经 BuildCartDtoAsync，应复用降级逻辑
        var cart = CreateAnonymousCartWithItemSelected();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.SaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AntiCorruptionException("product 防腐层超时", "PRODUCT_TIMEOUT"));

        // Act
        var result = await _sut.AddItemAsync(SessionId, new AddCartItemDto
        {
            SkuId = Guid.NewGuid(),
            Quantity = 1,
            SellerId = SellerId
        });

        // Assert：写操作路径同样进入降级分支
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.PriceUnavailable);
        result.Items.Should().OnlyContain(i => !i.Available);
        result.Items.Should().OnlyContain(i => i.Title == "[价格加载失败]");
        result.SelectedTotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task UpdateQuantityAsync_WhenPriceServiceThrowsAntiCorruptionException_ShouldDegrade()
    {
        // Arrange：UpdateQuantityAsync 路径同样经 BuildCartDtoAsync
        var cart = CreateAnonymousCartWithItemSelected();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.SaveAsync(SessionId, It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AntiCorruptionException("product 防腐层远程失败", "PRODUCT_REMOTE_FAILED"));

        // Act
        var result = await _sut.UpdateQuantityAsync(SessionId, SkuId, new UpdateCartItemQuantityDto { Quantity = 5 });

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].PriceUnavailable.Should().BeTrue();
        result.Items[0].Quantity.Should().Be(5);
        result.SelectedTotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task GetCartAsync_WhenPriceServiceThrowsAntiCorruptionException_ShouldNotRefreshTtl()
    {
        // Arrange：P2-8 协同——读操作不刷新 TTL；即使价格服务故障降级，TTL 行为不受影响
        var cart = CreateAnonymousCartWithItemSelected();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AntiCorruptionException("product 防腐层熔断", "PRODUCT_CIRCUIT_OPEN"));

        // Act
        await _sut.GetCartAsync(SessionId);

        // Assert：读操作不刷新 TTL（防攻击者定时 GET 让匿名购物车永久驻留）
        _repoMock.Verify(r => r.RefreshTtlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CartAggregate CreateAnonymousCartWithItemSelected()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.AddItem(SkuId, 2, SellerId);
        cart.SelectItems(new[] { SkuId });
        return cart;
    }
}
