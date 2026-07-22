using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// P1-14 多币种聚合错误回归测试。
/// 验证：
/// 1. GetCartAsync 混币种场景返回 SubtotalsByCurrency 多条目，SelectedTotalAmount 置 0（不抛异常，允许查看）
/// 2. GetCartAsync 单币种场景 SelectedTotalAmount 与 SubtotalsByCurrency 唯一条目一致
/// 3. PreviewCheckoutAsync 混币种场景抛 CART_MIXED_CURRENCY 阻止结算
/// 4. PreviewCheckoutAsync 单币种场景 TotalAmount 与 SubtotalsByCurrency 唯一条目一致
/// 5. AnonymousCartAppService 同步对齐
/// </summary>
public class MixedCurrencyAggregationTests
{
    #region CartAppService

    public class CartAppServiceMixedCurrencyTests
    {
        private readonly Mock<ICartRepository> _cartRepoMock = new();
        private readonly Mock<ICartPriceService> _priceServiceMock = new();
        private readonly Mock<IUnitOfWork> _uowMock = new();
        private readonly Mock<IAnonymousCartRepository> _anonymousCartRepoMock = new();
        private readonly Mock<ICartMergeRecordRepository> _cartMergeRecordRepoMock = new();
        private readonly Mock<ILogger<CartAppService>> _loggerMock = new();
        private readonly CartAppService _sut;

        private static readonly Guid UserId = Guid.NewGuid();
        private static readonly Guid SellerId = Guid.NewGuid();
        private static readonly Guid CnySkuId = Guid.NewGuid();
        private static readonly Guid UsdSkuId = Guid.NewGuid();

        public CartAppServiceMixedCurrencyTests()
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
        public async Task GetCartAsync_MixedCurrency_ShouldReturnSubtotalsByCurrencyAndZeroSelectedTotal()
        {
            // Arrange：两个选中项分别 CNY / USD，属于混币种场景
            var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
            cart.AddItem(CnySkuId, 2, SellerId);
            cart.AddItem(UsdSkuId, 1, SellerId);
            _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cart);
            SetupMixedCurrencyPrices();

            // Act
            var result = await _sut.GetCartAsync(UserId);

            // Assert：SubtotalsByCurrency 含两条目，SelectedTotalAmount 不再作为可结算依据（置 0）
            result.SubtotalsByCurrency.Should().HaveCount(2);
            result.SubtotalsByCurrency.Should().ContainKey("CNY").WhoseValue.Should().Be(19.9m * 2);
            result.SubtotalsByCurrency.Should().ContainKey("USD").WhoseValue.Should().Be(9.9m * 1);
            result.SelectedTotalAmount.Should().Be(0m);
            result.Currency.Should().Be("CNY");
        }

        [Fact]
        public async Task GetCartAsync_SingleCurrency_ShouldFillSelectedTotalAndSubtotalsByCurrency()
        {
            // Arrange：两个选中项均为 CNY，单币种场景
            var skuId2 = Guid.NewGuid();
            var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
            cart.AddItem(CnySkuId, 2, SellerId);
            cart.AddItem(skuId2, 1, SellerId);
            _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cart);
            _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                    ids.Select(id => new SkuPriceSnapshot
                    {
                        SkuId = id,
                        Price = 19.9m,
                        Currency = "CNY",
                        Available = true,
                        Title = "CNY 商品",
                        MainImageUrl = "https://img.example.com/cny.jpg",
                        SellerId = SellerId
                    }).ToList());

            // Act
            var result = await _sut.GetCartAsync(UserId);

            // Assert：单币种 SelectedTotalAmount 与 SubtotalsByCurrency 唯一条目一致
            result.SubtotalsByCurrency.Should().HaveCount(1);
            result.SubtotalsByCurrency.Should().ContainKey("CNY").WhoseValue.Should().Be(19.9m * 3);
            result.SelectedTotalAmount.Should().Be(19.9m * 3);
            result.Currency.Should().Be("CNY");
        }

        [Fact]
        public async Task PreviewCheckoutAsync_MixedCurrency_ShouldThrowCartMixedCurrency()
        {
            // Arrange：两个选中项分别 CNY / USD
            var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
            cart.AddItem(CnySkuId, 2, SellerId);
            cart.AddItem(UsdSkuId, 1, SellerId);
            _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cart);
            SetupMixedCurrencyPrices();

            // Act
            var act = () => _sut.PreviewCheckoutAsync(UserId);

            // Assert：混币种阻止结算
            await act.Should().ThrowAsync<CartDomainException>()
                .WithMessage("*跨币种合并结算*")
                .Where(ex => ex.ErrorCode == "CART_MIXED_CURRENCY");
        }

        [Fact]
        public async Task PreviewCheckoutAsync_SingleCurrency_ShouldFillTotalAndSubtotalsByCurrency()
        {
            // Arrange：两个选中项均为 CNY
            var skuId2 = Guid.NewGuid();
            var cart = CartAggregate.Create(Guid.NewGuid(), UserId);
            cart.AddItem(CnySkuId, 2, SellerId);
            cart.AddItem(skuId2, 1, SellerId);
            _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cart);
            _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                    ids.Select(id => new SkuPriceSnapshot
                    {
                        SkuId = id,
                        Price = 19.9m,
                        Currency = "CNY",
                        Available = true,
                        Title = "CNY 商品",
                        MainImageUrl = "https://img.example.com/cny.jpg",
                        SellerId = SellerId
                    }).ToList());

            // Act
            var result = await _sut.PreviewCheckoutAsync(UserId);

            // Assert：单币种正常返回，TotalAmount 与 SubtotalsByCurrency 唯一条目一致
            result.TotalAmount.Should().Be(19.9m * 3);
            result.Currency.Should().Be("CNY");
            result.SubtotalsByCurrency.Should().HaveCount(1);
            result.SubtotalsByCurrency.Should().ContainKey("CNY").WhoseValue.Should().Be(19.9m * 3);
        }

        private void SetupMixedCurrencyPrices()
        {
            _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                    ids.Select(id => new SkuPriceSnapshot
                    {
                        SkuId = id,
                        Price = id == CnySkuId ? 19.9m : 9.9m,
                        Currency = id == CnySkuId ? "CNY" : "USD",
                        Available = true,
                        Title = id == CnySkuId ? "CNY 商品" : "USD 商品",
                        MainImageUrl = "https://img.example.com/item.jpg",
                        SellerId = SellerId
                    }).ToList());
        }
    }

    #endregion

    #region AnonymousCartAppService

    public class AnonymousCartAppServiceMixedCurrencyTests
    {
        private readonly Mock<IAnonymousCartRepository> _repoMock = new();
        private readonly Mock<ICartPriceService> _priceMock = new();
        private readonly AnonymousCartAppService _sut;

        private const string SessionId = "session-mixed-1";
        private static readonly Guid CnySkuId = Guid.NewGuid();
        private static readonly Guid UsdSkuId = Guid.NewGuid();
        private static readonly Guid SellerId = Guid.NewGuid();

        public AnonymousCartAppServiceMixedCurrencyTests()
        {
            _sut = new AnonymousCartAppService(_repoMock.Object, _priceMock.Object);
        }

        [Fact]
        public async Task GetCartAsync_MixedCurrency_ShouldReturnSubtotalsByCurrencyAndZeroSelectedTotal()
        {
            // Arrange：匿名购物车两个选中项分别 CNY / USD
            var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
            cart.AddItem(CnySkuId, 2, SellerId);
            cart.AddItem(UsdSkuId, 1, SellerId);
            _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
            SetupMixedCurrencyPrices();

            // Act
            var result = await _sut.GetCartAsync(SessionId);

            // Assert
            result.SubtotalsByCurrency.Should().HaveCount(2);
            result.SubtotalsByCurrency.Should().ContainKey("CNY").WhoseValue.Should().Be(19.9m * 2);
            result.SubtotalsByCurrency.Should().ContainKey("USD").WhoseValue.Should().Be(9.9m * 1);
            result.SelectedTotalAmount.Should().Be(0m);
            result.Currency.Should().Be("CNY");
        }

        [Fact]
        public async Task PreviewCheckoutAsync_MixedCurrency_ShouldThrowCartMixedCurrency()
        {
            // Arrange
            var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
            cart.AddItem(CnySkuId, 2, SellerId);
            cart.AddItem(UsdSkuId, 1, SellerId);
            _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
            _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            SetupMixedCurrencyPrices();

            // Act
            var act = () => _sut.PreviewCheckoutAsync(SessionId);

            // Assert
            await act.Should().ThrowAsync<CartDomainException>()
                .WithMessage("*跨币种合并结算*")
                .Where(ex => ex.ErrorCode == "CART_MIXED_CURRENCY");
        }

        private void SetupMixedCurrencyPrices()
        {
            _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                    ids.Select(id => new SkuPriceSnapshot
                    {
                        SkuId = id,
                        Price = id == CnySkuId ? 19.9m : 9.9m,
                        Currency = id == CnySkuId ? "CNY" : "USD",
                        Available = true,
                        Title = id == CnySkuId ? "CNY 商品" : "USD 商品",
                        MainImageUrl = "https://img.example.com/item.jpg",
                        SellerId = SellerId
                    }).ToList());
        }
    }

    #endregion
}
