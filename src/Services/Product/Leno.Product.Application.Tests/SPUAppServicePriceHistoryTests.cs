using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Services;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.Services;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;
using Moq;

namespace Leno.Product.Application.Tests;

/// <summary>
/// P1-T13 单元测试：验证 <see cref="SPUAppService.AdjustPriceAsync"/> 将 changedBy 与 dto.Reason
/// 透传给 <see cref="PriceHistory.Create"/>，且 <see cref="SPUAppService.GetPriceHistoryAsync"/>
/// 返回真实 ChangedBy 与 Reason（替代原硬编码 string.Empty）。
/// 修复审计 #13：原 AdjustPriceAsync 传 reason: null，ToPriceChangeRecordDto 硬编码 ChangedBy = string.Empty。
/// </summary>
public class SPUAppServicePriceHistoryTests
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

    public SPUAppServicePriceHistoryTests()
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
    /// AdjustPriceAsync 应将 changedBy 与 dto.Reason 透传给 PriceHistory.Create。
    /// </summary>
    [Fact]
    public async Task AdjustPriceAsync_ShouldPassChangedByAndReasonToPriceHistory()
    {
        // Arrange
        var spu = CreateSpuWithSku(SellerId, ShopId);
        var sku = spu.SKUs.First();
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        PriceHistory? capturedHistory = null;
        _priceHistoryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PriceHistory>(), It.IsAny<CancellationToken>()))
            .Callback<PriceHistory, CancellationToken>((h, _) => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var dto = new AdjustPriceDto
        {
            Price = 79.99m,
            Currency = "CNY",
            Reason = "双 11 促销调价"
        };

        // Act
        await _sut.AdjustPriceAsync(spu.Id, sku.Id, dto, "seller-001", CancellationToken.None);

        // Assert — PriceHistory 应捕获到正确的 changedBy 和 reason
        capturedHistory.Should().NotBeNull();
        capturedHistory!.ChangedBy.Should().Be("seller-001", "changedBy 应从应用层透传到 PriceHistory");
        capturedHistory.Reason.Should().Be("双 11 促销调价", "dto.Reason 应透传到 PriceHistory");
        capturedHistory.OldPrice.Should().Be(99.99m);
        capturedHistory.NewPrice.Should().Be(79.99m);
    }

    /// <summary>
    /// AdjustPriceAsync 在 dto.Reason 为 null 时，PriceHistory.Reason 应为 null（保持向后兼容）。
    /// </summary>
    [Fact]
    public async Task AdjustPriceAsync_WithNullReason_ShouldPassNullToPriceHistory()
    {
        // Arrange
        var spu = CreateSpuWithSku(SellerId, ShopId);
        var sku = spu.SKUs.First();
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        PriceHistory? capturedHistory = null;
        _priceHistoryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PriceHistory>(), It.IsAny<CancellationToken>()))
            .Callback<PriceHistory, CancellationToken>((h, _) => capturedHistory = h)
            .Returns(Task.CompletedTask);

        var dto = new AdjustPriceDto
        {
            Price = 89.99m,
            Currency = "CNY",
            Reason = null
        };

        // Act
        await _sut.AdjustPriceAsync(spu.Id, sku.Id, dto, "admin-002", CancellationToken.None);

        // Assert
        capturedHistory.Should().NotBeNull();
        capturedHistory!.ChangedBy.Should().Be("admin-002");
        capturedHistory.Reason.Should().BeNull();
    }

    /// <summary>
    /// GetPriceHistoryAsync 应返回 PriceHistory 中的真实 ChangedBy（非空字符串）。
    /// </summary>
    [Fact]
    public async Task GetPriceHistoryAsync_ShouldReturnRealChangedBy()
    {
        // Arrange
        var spu = CreateDraftSpu(SellerId, ShopId);
        var skuId = Guid.NewGuid();
        var history = PriceHistory.Create(
            spu.Id, skuId, 100m, 80m,
            reason: "清仓", currency: "CNY", changedBy: "manager-003");

        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);
        _priceHistoryRepoMock.Setup(r => r.GetBySpuIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PriceHistory> { history });

        // Act
        var result = await _sut.GetPriceHistoryAsync(spu.Id, null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var record = result.First();
        record.ChangedBy.Should().Be("manager-003", "应返回真实变更人而非空字符串");
        record.Reason.Should().Be("清仓");
        record.SkuId.Should().Be(skuId.ToString());
        record.OldPrice.Should().Be(100m);
        record.NewPrice.Should().Be(80m);
    }

    /// <summary>
    /// GetPriceHistoryAsync 在 PriceHistory.ChangedBy 为空（向后兼容旧数据）时返回空字符串。
    /// </summary>
    [Fact]
    public async Task GetPriceHistoryAsync_WithEmptyChangedBy_ReturnsEmptyString()
    {
        // Arrange
        var spu = CreateDraftSpu(SellerId, ShopId);
        var history = PriceHistory.Create(
            spu.Id, Guid.NewGuid(), 100m, 90m); // 不传 changedBy，默认空

        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spu);
        _priceHistoryRepoMock.Setup(r => r.GetBySpuIdAsync(spu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PriceHistory> { history });

        // Act
        var result = await _sut.GetPriceHistoryAsync(spu.Id, null, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().ChangedBy.Should().BeEmpty();
    }

    private static SPU CreateDraftSpu(Guid sellerId, Guid shopId)
    {
        return SPU.Create(Guid.NewGuid(), shopId, sellerId, "Test Product",
            "https://img.example.com/1.jpg", CategoryId, images: new List<ProductImage>());
    }

    private static SPU CreateSpuWithSku(Guid sellerId, Guid shopId)
    {
        var spu = CreateDraftSpu(sellerId, shopId);
        var sku = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100,
            SkuSpec.Create(new[] { SpecAttribute.Create("Color", "Red") }));
        spu.AddSku(sku);
        return spu;
    }
}
