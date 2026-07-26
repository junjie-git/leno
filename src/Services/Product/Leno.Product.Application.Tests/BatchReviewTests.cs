using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Services;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.Services;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Leno.SharedKernel.ValueObjects;
using Moq;

namespace Leno.Product.Application.Tests;

/// <summary>
/// 批量审核（BatchApproveAsync / BatchRejectAsync）单元测试。
/// 验证批量操作正确处理成功与部分失败场景，单个失败不阻塞整批。
/// </summary>
public class BatchReviewTests
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
    private static readonly Guid ReviewerId = Guid.NewGuid();

    public BatchReviewTests()
    {
        _actionReasonValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ActionReasonDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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

    #region BatchApproveAsync

    [Fact]
    public async Task BatchApproveAsync_AllSucceed_ShouldReturnAllSucceededIds()
    {
        var spu1 = CreatePendingReviewSpu();
        var spu2 = CreatePendingReviewSpu();
        var spu3 = CreatePendingReviewSpu();
        var ids = new List<Guid> { spu1.Id, spu2.Id, spu3.Id };
        SetupSpuLookup(new[] { spu1, spu2, spu3 });

        var result = await _sut.BatchApproveAsync(ids, ReviewerId, reason: null);

        result.SucceededIds.Should().BeEquivalentTo(ids);
        result.Failures.Should().BeEmpty();
        spu1.Status.Should().Be(ProductStatus.OnSale);
        spu2.Status.Should().Be(ProductStatus.OnSale);
        spu3.Status.Should().Be(ProductStatus.OnSale);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task BatchApproveAsync_PartialFailure_ShouldCollectSucceededAndFailures()
    {
        var spu1 = CreatePendingReviewSpu();
        var spu2 = CreateDraftSpu();
        var spu3 = CreatePendingReviewSpu();
        var notFoundId = Guid.NewGuid();
        var ids = new List<Guid> { spu1.Id, spu2.Id, spu3.Id, notFoundId };
        SetupSpuLookup(new[] { spu1, spu2, spu3 });

        var result = await _sut.BatchApproveAsync(ids, ReviewerId, reason: null);

        result.SucceededIds.Should().BeEquivalentTo(new[] { spu1.Id, spu3.Id });
        result.Failures.Should().HaveCount(2);
        result.Failures.Should().Contain(f => f.Id == spu2.Id);
        result.Failures.Should().Contain(f => f.Id == notFoundId);
        spu1.Status.Should().Be(ProductStatus.OnSale);
        spu3.Status.Should().Be(ProductStatus.OnSale);
        spu2.Status.Should().Be(ProductStatus.Draft);
    }

    [Fact]
    public async Task BatchApproveAsync_EmptyIds_ShouldReturnEmptyResult()
    {
        var result = await _sut.BatchApproveAsync(new List<Guid>(), ReviewerId, reason: null);

        result.SucceededIds.Should().BeEmpty();
        result.Failures.Should().BeEmpty();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region BatchRejectAsync

    [Fact]
    public async Task BatchRejectAsync_AllSucceed_ShouldReturnAllSucceededIds()
    {
        var spu1 = CreatePendingReviewSpu();
        var spu2 = CreatePendingReviewSpu();
        var ids = new List<Guid> { spu1.Id, spu2.Id };
        SetupSpuLookup(new[] { spu1, spu2 });

        var result = await _sut.BatchRejectAsync(ids, ReviewerId, reason: "信息不完整");

        result.SucceededIds.Should().BeEquivalentTo(ids);
        result.Failures.Should().BeEmpty();
        spu1.Status.Should().Be(ProductStatus.Rejected);
        spu2.Status.Should().Be(ProductStatus.Rejected);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BatchRejectAsync_PartialFailure_ShouldCollectSucceededAndFailures()
    {
        var spu1 = CreatePendingReviewSpu();
        var spu2 = CreateDraftSpu();
        var spu3 = CreatePendingReviewSpu();
        var notFoundId = Guid.NewGuid();
        var ids = new List<Guid> { spu1.Id, spu2.Id, spu3.Id, notFoundId };
        SetupSpuLookup(new[] { spu1, spu2, spu3 });

        var result = await _sut.BatchRejectAsync(ids, ReviewerId, reason: "信息不完整");

        result.SucceededIds.Should().BeEquivalentTo(new[] { spu1.Id, spu3.Id });
        result.Failures.Should().HaveCount(2);
        result.Failures.Should().Contain(f => f.Id == spu2.Id);
        result.Failures.Should().Contain(f => f.Id == notFoundId);
        spu1.Status.Should().Be(ProductStatus.Rejected);
        spu3.Status.Should().Be(ProductStatus.Rejected);
        spu2.Status.Should().Be(ProductStatus.Draft);
    }

    [Fact]
    public async Task BatchRejectAsync_EmptyReason_ShouldThrow()
    {
        var ids = new List<Guid> { Guid.NewGuid() };

        var act = () => _sut.BatchRejectAsync(ids, ReviewerId, reason: "  ");

        await act.Should().ThrowAsync<ProductDomainException>();
    }

    [Fact]
    public async Task BatchRejectAsync_EmptyIds_ShouldReturnEmptyResult()
    {
        var result = await _sut.BatchRejectAsync(new List<Guid>(), ReviewerId, reason: "test");

        result.SucceededIds.Should().BeEmpty();
        result.Failures.Should().BeEmpty();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Helpers

    private void SetupSpuLookup(IEnumerable<SPU> spus)
    {
        var list = spus.ToList();
        _spuRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                list.FirstOrDefault(s => s.Id == id));
    }

    private static SPU CreateDraftSpu()
    {
        var spu = SPU.Create(Guid.NewGuid(), ShopId, SellerId, "Test Product",
            "https://img.example.com/1.jpg", CategoryId, images: new List<ProductImage>());
        return spu;
    }

    private static SPU CreatePendingReviewSpu()
    {
        var spu = CreateDraftSpu();
        var sku = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Money.Create(99.99m, "CNY"), 100,
            SkuSpec.Create(new[] { SpecAttribute.Create("Color", "Red") }));
        spu.AddSku(sku);
        spu.SubmitForReview();
        return spu;
    }

    #endregion
}
