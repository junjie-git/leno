using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Services;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.Services;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.Product.Application.Tests;

/// <summary>
/// P2-T19 单元测试：验证 <see cref="SPUAppService.GetPriceHistoryAsync"/> 返回的
/// <see cref="PriceChangeRecordDto"/> 正确映射 <see cref="PriceHistory.Currency"/> 字段。
/// 修复审计 #19：原 <c>PriceChangeRecordDto</c> 缺少 Currency 字段，<c>ToPriceChangeRecordDto</c>
/// 未映射币种，导致 API 响应无法区分多币种价格变更记录。
/// </summary>
public class PriceChangeRecordDtoCurrencyTests
{
    private readonly Mock<ISPURepository> _spuRepoMock = new();
    private readonly Mock<IPriceHistoryRepository> _priceHistoryRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IProductUniquenessChecker> _uniquenessCheckerMock = new();
    private readonly Mock<IValidator<CreateProductDto>> _createValidatorMock = new();
    private readonly Mock<IValidator<UpdateProductDto>> _updateValidatorMock = new();
    private readonly Mock<IValidator<AddSkuDto>> _addSkuValidatorMock = new();
    private readonly Mock<IValidator<ActionReasonDto>> _actionReasonValidatorMock = new();
    private readonly SPUAppService _sut;

    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public PriceChangeRecordDtoCurrencyTests()
    {
        _sut = new SPUAppService(
            _spuRepoMock.Object,
            _priceHistoryRepoMock.Object,
            _uowMock.Object,
            _uniquenessCheckerMock.Object,
            _createValidatorMock.Object,
            _updateValidatorMock.Object,
            _addSkuValidatorMock.Object,
            _actionReasonValidatorMock.Object);
    }

    /// <summary>
    /// GetPriceHistoryAsync 应将 PriceHistory.Currency 映射到 PriceChangeRecordDto.Currency，
    /// 非默认币种（如 USD）应正确传递。
    /// </summary>
    [Fact]
    public async Task GetPriceHistoryAsync_ShouldMapCurrencyFromPriceHistory()
    {
        // Arrange
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var history = PriceHistory.Create(
            spu.Id, skuId, 100m, 80m,
            reason: "促销调价", currency: "USD", changedBy: "seller-001");

        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);
        _priceHistoryRepoMock.Setup(r => r.GetBySpuIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PriceHistory> { history });

        // Act
        var result = await _sut.GetPriceHistoryAsync(spu.Id, null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var record = result.First();
        record.Currency.Should().Be("USD", "应从 PriceHistory.Currency 映射币种");
        record.SkuId.Should().Be(skuId.ToString());
        record.OldPrice.Should().Be(100m);
        record.NewPrice.Should().Be(80m);
        record.ChangedBy.Should().Be("seller-001");
        record.Reason.Should().Be("促销调价");
    }

    /// <summary>
    /// 默认币种（CNY）的 PriceHistory 映射后 Currency 应为 "CNY"。
    /// </summary>
    [Fact]
    public async Task GetPriceHistoryAsync_WithDefaultCurrency_ShouldMapCNY()
    {
        // Arrange — 不传 currency，PriceHistory.Create 默认 "CNY"
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var history = PriceHistory.Create(
            spu.Id, skuId, 50m, 45m,
            changedBy: "admin-002");

        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);
        _priceHistoryRepoMock.Setup(r => r.GetBySpuIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PriceHistory> { history });

        // Act
        var result = await _sut.GetPriceHistoryAsync(spu.Id, null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Currency.Should().Be("CNY", "默认币种应为 CNY");
    }

    /// <summary>
    /// 多币种价格变更记录应各自保留正确的 Currency 字段。
    /// </summary>
    [Fact]
    public async Task GetPriceHistoryAsync_MultiCurrencyRecords_ShouldPreserveEachCurrency()
    {
        // Arrange — 2 条不同币种的价格变更记录
        var spu = CreateDraftSpu();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        var history1 = PriceHistory.Create(
            spu.Id, skuId1, 100m, 90m, currency: "CNY", changedBy: "seller-001");
        var history2 = PriceHistory.Create(
            spu.Id, skuId2, 20m, 18m, currency: "USD", changedBy: "seller-001");

        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);
        _priceHistoryRepoMock.Setup(r => r.GetBySpuIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PriceHistory> { history1, history2 });

        // Act
        var result = await _sut.GetPriceHistoryAsync(spu.Id, null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        var cnyRecord = result.First(r => r.SkuId == skuId1.ToString());
        cnyRecord.Currency.Should().Be("CNY");
        cnyRecord.OldPrice.Should().Be(100m);
        cnyRecord.NewPrice.Should().Be(90m);

        var usdRecord = result.First(r => r.SkuId == skuId2.ToString());
        usdRecord.Currency.Should().Be("USD");
        usdRecord.OldPrice.Should().Be(20m);
        usdRecord.NewPrice.Should().Be(18m);
    }

    /// <summary>
    /// 按 skuId 过滤后，仍应正确映射 Currency 字段。
    /// </summary>
    [Fact]
    public async Task GetPriceHistoryAsync_FilterBySkuId_ShouldMapCurrencyForFilteredRecord()
    {
        // Arrange
        var spu = CreateDraftSpu();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        var history1 = PriceHistory.Create(
            spu.Id, skuId1, 100m, 90m, currency: "EUR", changedBy: "seller-001");
        var history2 = PriceHistory.Create(
            spu.Id, skuId2, 50m, 45m, currency: "CNY", changedBy: "seller-001");

        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);
        _priceHistoryRepoMock.Setup(r => r.GetBySpuIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PriceHistory> { history1, history2 });

        // Act — 按 skuId1 过滤
        var result = await _sut.GetPriceHistoryAsync(spu.Id, skuId1, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var record = result.First();
        record.SkuId.Should().Be(skuId1.ToString());
        record.Currency.Should().Be("EUR", "过滤后仍应正确映射币种");
    }

    private static SPU CreateDraftSpu()
    {
        return SPU.Create(Guid.NewGuid(), ShopId, SellerId, "Test Product",
            "https://img.example.com/1.jpg", CategoryId, images: new List<ProductImage>());
    }
}
