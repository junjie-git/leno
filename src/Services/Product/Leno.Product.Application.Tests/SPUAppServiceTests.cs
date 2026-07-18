using FluentValidation;
using Leno.Product.Application.DTOs;
using Leno.Product.Application.Exceptions;
using Leno.Product.Application.Services;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.Repositories;
using Leno.Product.Domain.Services;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.Product.Application.Tests;

public class SPUAppServiceTests
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

    public SPUAppServiceTests()
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

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidInput_ShouldReturnProductDto()
    {
        SetupValidValidation(_createValidatorMock);
        SetupTitleUnique();
        var dto = CreateValidCreateProductDto();

        var result = await _sut.CreateAsync(SellerId, ShopId, dto);

        result.Should().NotBeNull();
        result.Title.Should().Be("Test Product");
        result.SellerId.Should().Be(SellerId);
        result.ShopId.Should().Be(ShopId);
        result.Status.Should().Be(ProductStatus.Draft);
        _spuRepoMock.Verify(r => r.AddAsync(It.IsAny<SPU>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicateTitle_ShouldThrowException()
    {
        SetupValidValidation(_createValidatorMock);
        SetupTitleNotUnique();
        var dto = CreateValidCreateProductDto();

        var act = () => _sut.CreateAsync(SellerId, ShopId, dto);

        await act.Should().ThrowAsync<ProductDomainException>()
            .WithMessage("*title already exists*");
    }

    [Fact]
    public async Task CreateAsync_EmptySellerId_ShouldThrowException()
    {
        SetupValidValidation(_createValidatorMock);
        var dto = CreateValidCreateProductDto();

        var act = () => _sut.CreateAsync(Guid.Empty, ShopId, dto);

        await act.Should().ThrowAsync<ProductDomainException>().WithMessage("*卖家*");
    }

    [Fact]
    public async Task CreateAsync_EmptyShopId_ShouldThrowException()
    {
        SetupValidValidation(_createValidatorMock);
        var dto = CreateValidCreateProductDto();

        var act = () => _sut.CreateAsync(SellerId, Guid.Empty, dto);

        await act.Should().ThrowAsync<ProductDomainException>().WithMessage("*店铺*");
    }

    [Fact]
    public async Task CreateAsync_ValidationFailure_ShouldThrowProductValidationException()
    {
        SetupFailedValidation(_createValidatorMock);

        var act = () => _sut.CreateAsync(SellerId, ShopId, CreateValidCreateProductDto());

        await act.Should().ThrowAsync<ProductValidationException>();
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ValidInput_ShouldUpdateAndReturnDto()
    {
        SetupValidValidation(_updateValidatorMock);
        SetupTitleUnique();
        var spu = CreateDraftSpu(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);
        var dto = new UpdateProductDto
        {
            Title = "Updated Title",
            MainImageUrl = "https://img.example.com/2.jpg",
            CategoryId = CategoryId,
            Images = new List<ProductImageDto>()
        };

        var result = await _sut.UpdateAsync(SellerId, spu.Id, dto);

        result.Title.Should().Be("Updated Title");
        _spuRepoMock.Verify(r => r.UpdateAsync(spu, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateTitle_ShouldThrowException()
    {
        SetupValidValidation(_updateValidatorMock);
        SetupTitleNotUnique();
        var spu = CreateDraftSpu(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);
        var dto = new UpdateProductDto
        {
            Title = "Duplicate Title",
            MainImageUrl = "https://img.example.com/2.jpg",
            CategoryId = CategoryId,
            Images = new List<ProductImageDto>()
        };

        var act = () => _sut.UpdateAsync(SellerId, spu.Id, dto);

        await act.Should().ThrowAsync<ProductDomainException>()
            .WithMessage("*title already exists*");
    }

    [Fact]
    public async Task UpdateAsync_NotOwned_ShouldThrowException()
    {
        SetupValidValidation(_updateValidatorMock);
        SetupTitleUnique();
        var spu = CreateDraftSpu(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);
        var otherSeller = Guid.NewGuid();
        var dto = new UpdateProductDto
        {
            Title = "Updated",
            MainImageUrl = "https://img.example.com/2.jpg",
            CategoryId = CategoryId,
            Images = new List<ProductImageDto>()
        };

        var act = () => _sut.UpdateAsync(otherSeller, spu.Id, dto);

        await act.Should().ThrowAsync<ProductDomainException>().WithMessage("*无权*");
    }

    #endregion

    #region AddSkuAsync

    [Fact]
    public async Task AddSkuAsync_ValidInput_ShouldAddSku()
    {
        SetupValidValidation(_addSkuValidatorMock);
        SetupSkuCodeUnique();
        var spu = CreateDraftSpu(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);
        var dto = new AddSkuDto
        {
            SkuCode = "SKU-002",
            Price = 199.99m,
            Currency = "CNY",
            StockQty = 50,
            SpecAttributes = new List<SpecAttributeDto>
            {
                new() { Name = "Color", Value = "Blue" }
            }
        };

        var result = await _sut.AddSkuAsync(SellerId, spu.Id, dto);

        result.Skus.Should().HaveCount(1);
        _spuRepoMock.Verify(r => r.UpdateAsync(spu, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddSkuAsync_DuplicateSkuCode_ShouldThrowException()
    {
        SetupValidValidation(_addSkuValidatorMock);
        SetupSkuCodeNotUnique();
        var spu = CreateDraftSpu(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);
        var dto = new AddSkuDto
        {
            SkuCode = "SKU-001",
            Price = 199.99m,
            Currency = "CNY",
            StockQty = 50,
            SpecAttributes = new List<SpecAttributeDto>
            {
                new() { Name = "Color", Value = "Blue" }
            }
        };

        var act = () => _sut.AddSkuAsync(SellerId, spu.Id, dto);

        await act.Should().ThrowAsync<ProductDomainException>()
            .WithMessage("*SKU code already in use*");
    }

    #endregion

    #region SubmitForReviewAsync

    [Fact]
    public async Task SubmitForReviewAsync_ValidTransition_ShouldSucceed()
    {
        var spu = CreateSpuWithSku(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);

        await _sut.SubmitForReviewAsync(SellerId, spu.Id);

        spu.Status.Should().Be(ProductStatus.PendingReview);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ApproveAsync

    [Fact]
    public async Task ApproveAsync_ValidInput_ShouldTransitionToOnSale()
    {
        var spu = CreateSpuWithSku(SellerId, ShopId);
        spu.SubmitForReview();
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);

        await _sut.ApproveAsync(spu.Id, Guid.NewGuid());

        spu.Status.Should().Be(ProductStatus.OnSale);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_EmptyReviewerId_ShouldThrowException()
    {
        var spu = CreateSpuWithSku(SellerId, ShopId);
        spu.SubmitForReview();
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);

        var act = () => _sut.ApproveAsync(spu.Id, Guid.Empty);

        await act.Should().ThrowAsync<ProductDomainException>();
    }

    #endregion

    #region RejectAsync

    [Fact]
    public async Task RejectAsync_ValidInput_ShouldTransitionToRejected()
    {
        SetupValidValidation(_actionReasonValidatorMock);
        var spu = CreateSpuWithSku(SellerId, ShopId);
        spu.SubmitForReview();
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);
        var dto = new ActionReasonDto { Reason = "Not good enough" };

        await _sut.RejectAsync(spu.Id, Guid.NewGuid(), dto);

        spu.Status.Should().Be(ProductStatus.Rejected);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ExistingSpu_ShouldReturnProductDto()
    {
        var spu = CreateDraftSpu(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.GetByIdAsync(spu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(spu);

        var result = await _sut.GetByIdAsync(spu.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(spu.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ShouldThrowException()
    {
        _spuRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((SPU?)null);

        var act = () => _sut.GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ProductDomainException>().WithMessage("*商品*");
    }

    #endregion

    #region QueryProductsAsync

    [Fact]
    public async Task QueryProductsAsync_ValidQuery_ShouldReturnPagedResult()
    {
        var spu = CreateDraftSpu(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.QueryAsync(
                null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SPU> { spu }, 1));
        var query = new ProductQueryDto();

        var result = await _sut.QueryProductsAsync(query);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task QueryProductsAsync_WithSellerIdFilter_ShouldPassFilterToRepo()
    {
        var spu = CreateDraftSpu(SellerId, ShopId);
        var targetSellerId = Guid.NewGuid();
        _spuRepoMock.Setup(r => r.QueryAsync(
                null, targetSellerId, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SPU> { spu }, 1));
        var query = new ProductQueryDto { SellerId = targetSellerId };

        var result = await _sut.QueryProductsAsync(query);

        result.Items.Should().HaveCount(1);
        _spuRepoMock.Verify(r => r.QueryAsync(
            null, targetSellerId, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryProductsAsync_WithStatusFilter_ShouldParseAndPassFilter()
    {
        var spu = CreateDraftSpu(SellerId, ShopId);
        _spuRepoMock.Setup(r => r.QueryAsync(
                null, null, ProductStatus.Draft, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SPU> { spu }, 1));
        var query = new ProductQueryDto { Status = "Draft" };

        var result = await _sut.QueryProductsAsync(query);

        result.Items.Should().HaveCount(1);
        _spuRepoMock.Verify(r => r.QueryAsync(
            null, null, ProductStatus.Draft, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    private static void SetupValidValidation<T>(Mock<IValidator<T>> validatorMock)
    {
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
    }

    private static void SetupFailedValidation<T>(Mock<IValidator<T>> validatorMock)
    {
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(
                new[] { new FluentValidation.Results.ValidationFailure("Title", "Title is required") }));
    }

    private void SetupTitleUnique()
    {
        _uniquenessCheckerMock.Setup(c => c.IsTitleUniqueInShopAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupTitleNotUnique()
    {
        _uniquenessCheckerMock.Setup(c => c.IsTitleUniqueInShopAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private void SetupSkuCodeUnique()
    {
        _uniquenessCheckerMock.Setup(c => c.IsSkuCodeUniqueAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupSkuCodeNotUnique()
    {
        _uniquenessCheckerMock.Setup(c => c.IsSkuCodeUniqueAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static CreateProductDto CreateValidCreateProductDto()
    {
        return new CreateProductDto
        {
            Title = "Test Product",
            MainImageUrl = "https://img.example.com/1.jpg",
            CategoryId = CategoryId,
            Images = new List<ProductImageDto>()
        };
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
            Leno.SharedKernel.ValueObjects.Money.Create(99.99m, "CNY"), 100,
            SkuSpec.Create(new[] { Leno.SharedKernel.ValueObjects.SpecAttribute.Create("Color", "Red") }));
        spu.AddSku(sku);
        return spu;
    }
}