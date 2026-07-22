using FluentValidation;
using Leno.Infrastructure.Abstractions;
using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.SellerShop.Application.Tests;

/// <summary>
/// 店铺管理应用服务单元测试，覆盖入驻申请、审核、信息维护、状态流转与查询用例。
/// </summary>
public class ShopAppServiceTests
{
    private readonly Mock<IShopRepository> _shopRepoMock = new();
    private readonly Mock<ISellerProfileRepository> _profileRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<IValidator<SubmitShopApplicationDto>> _submitValidatorMock = new();
    private readonly Mock<IValidator<UpdateShopInfoDto>> _updateValidatorMock = new();
    private readonly Mock<IValidator<ActionReasonDto>> _actionReasonValidatorMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyStoreMock = new();
    private readonly ShopAppService _sut;

    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();

    public ShopAppServiceTests()
    {
        _submitValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SubmitShopApplicationDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _updateValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateShopInfoDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _actionReasonValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ActionReasonDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _sut = new ShopAppService(
            _shopRepoMock.Object,
            _profileRepoMock.Object,
            _uowMock.Object,
            _fileStorageMock.Object,
            _submitValidatorMock.Object,
            _updateValidatorMock.Object,
            _actionReasonValidatorMock.Object,
            _idempotencyStoreMock.Object);
    }

    [Fact]
    public async Task SubmitShopApplicationAsync_NewSeller_ShouldCreateShopAndProfile()
    {
        var dto = BuildSubmitDto();
        _shopRepoMock
            .Setup(r => r.GetBySellerIdAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shop?)null);

        var result = await _sut.SubmitShopApplicationAsync(SellerId, dto);

        result.ShopName.Should().Be("Leno 旗舰店");
        result.SellerId.Should().Be(SellerId);
        result.Status.Should().Be(ShopStatus.PendingReview);
        _shopRepoMock.Verify(r => r.AddAsync(It.IsAny<Shop>(), It.IsAny<CancellationToken>()), Times.Once);
        // 注：Shop.Create 工厂已发布 SellerRegisteredEvent，AppService 入驻流程未额外校验日志记录
        _profileRepoMock.Verify(r => r.AddAsync(It.IsAny<SellerProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitShopApplicationAsync_AlreadyExists_ShouldThrowDomainException()
    {
        var dto = BuildSubmitDto();
        var existing = Shop.Create(ShopId, SellerId, "已有店", "13800000000");
        _shopRepoMock
            .Setup(r => r.GetBySellerIdAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var act = () => _sut.SubmitShopApplicationAsync(SellerId, dto);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*已提交入驻申请*");
        _shopRepoMock.Verify(r => r.AddAsync(It.IsAny<Shop>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveShopApplicationAsync_PendingReview_ShouldApproveAndSave()
    {
        var shop = CreatePendingShop();
        _shopRepoMock
            .Setup(r => r.GetByIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);

        await _sut.ApproveShopApplicationAsync(ShopId, ReviewerId);

        shop.Status.Should().Be(ShopStatus.Active);
        shop.ReviewedBy.Should().Be(ReviewerId);
        _shopRepoMock.Verify(r => r.UpdateAsync(shop, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectShopApplicationAsync_PendingReview_ShouldRejectWithReason()
    {
        var shop = CreatePendingShop();
        _shopRepoMock
            .Setup(r => r.GetByIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);

        await _sut.RejectShopApplicationAsync(ShopId, ReviewerId, new ActionReasonDto { Reason = "材料不合规" });

        shop.Status.Should().Be(ShopStatus.Rejected);
        shop.StatusReason.Should().Be("材料不合规");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuspendShopAsync_ActiveShop_ShouldSuspend()
    {
        var shop = CreateActiveShop();
        _shopRepoMock
            .Setup(r => r.GetByIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);

        await _sut.SuspendShopAsync(ShopId, new ActionReasonDto { Reason = "违规经营" });

        shop.Status.Should().Be(ShopStatus.Suspended);
        shop.StatusReason.Should().Be("违规经营");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeShopAsync_SuspendedShop_ShouldResume()
    {
        var shop = CreateSuspendedShop();
        _shopRepoMock
            .Setup(r => r.GetByIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);

        await _sut.ResumeShopAsync(ShopId);

        shop.Status.Should().Be(ShopStatus.Active);
        shop.StatusReason.Should().Be(null);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseShopAsync_ActiveShop_ShouldClose()
    {
        var shop = CreateActiveShop();
        _shopRepoMock
            .Setup(r => r.GetByIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);

        await _sut.CloseShopAsync(ShopId, new ActionReasonDto { Reason = "主动关店" });

        shop.Status.Should().Be(ShopStatus.Closed);
        shop.StatusReason.Should().Be("主动关店");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateShopInfoAsync_Existing_ShouldUpdateFields()
    {
        var shop = CreateActiveShop();
        _shopRepoMock
            .Setup(r => r.GetByIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);

        var result = await _sut.UpdateShopInfoAsync(ShopId, new UpdateShopInfoDto
        {
            ShopName = "新店名",
            Description = "新描述",
            Address = "新地址",
            Logo = "https://cdn/logo.png",
            ContactPhone = "13900000000",
            ContactEmail = "new@example.com"
        });

        result.ShopName.Should().Be("新店名");
        shop.ShopName.Should().Be("新店名");
        shop.Description.Should().Be("新描述");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetShopInfoAsync_Existing_ShouldReturnDto()
    {
        var shop = CreateActiveShop();
        _shopRepoMock
            .Setup(r => r.GetByIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);

        var result = await _sut.GetShopInfoAsync(ShopId);

        result.Id.Should().Be(ShopId);
        result.ShopName.Should().Be("Leno 旗舰店");
    }

    [Fact]
    public async Task GetShopInfoAsync_NotFound_ShouldThrowDomainException()
    {
        _shopRepoMock
            .Setup(r => r.GetByIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shop?)null);

        var act = () => _sut.GetShopInfoAsync(ShopId);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*店铺不存在*");
    }

    [Fact]
    public async Task GetMyShopAsync_Existing_ShouldReturnDto()
    {
        var shop = CreateActiveShop();
        _shopRepoMock
            .Setup(r => r.GetBySellerIdAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shop);

        var result = await _sut.GetMyShopAsync(SellerId);

        result.SellerId.Should().Be(SellerId);
    }

    [Fact]
    public async Task GetMyShopAsync_EmptySellerId_ShouldThrowDomainException()
    {
        var act = () => _sut.GetMyShopAsync(Guid.Empty);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*卖家账号标识不可为空*");
    }

    [Fact]
    public async Task QueryShopsAsync_WithStatus_ShouldParseAndQuery()
    {
        var shops = new List<Shop> { CreateActiveShop() };
        _shopRepoMock
            .Setup(r => r.QueryAsync(ShopStatus.Active, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((shops, 1));

        var result = await _sut.QueryShopsAsync(new AdminShopQueryDto
        {
            Status = "Active",
            Page = 1,
            PageSize = 20
        });

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        _shopRepoMock.Verify(r => r.QueryAsync(ShopStatus.Active, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryShopsAsync_InvalidStatus_ShouldQueryAllStatus()
    {
        var shops = new List<Shop> { CreateActiveShop() };
        _shopRepoMock
            .Setup(r => r.QueryAsync(null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((shops, 1));

        var result = await _sut.QueryShopsAsync(new AdminShopQueryDto
        {
            Status = "NotARealStatus",
            Page = 1,
            PageSize = 20
        });

        result.Items.Should().HaveCount(1);
        _shopRepoMock.Verify(r => r.QueryAsync(null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SubmitShopApplicationDto BuildSubmitDto() => new()
    {
        ShopName = "Leno 旗舰店",
        ContactPhone = "13800000000",
        ContactEmail = "shop@example.com",
        Description = "Leno 自营旗舰店",
        Logo = null,
        Address = "北京市朝阳区",
        BusinessLicenseNo = "91110000123456789X",
        RealName = "张三",
        IdCard = "110101199001011234",
        BankAccount = "6222021234567890123"
    };

    private static Shop CreatePendingShop() =>
        Shop.Create(ShopId, SellerId, "Leno 旗舰店", "13800000000");

    private static Shop CreateActiveShop()
    {
        var shop = CreatePendingShop();
        shop.Approve(ReviewerId);
        return shop;
    }

    private static Shop CreateSuspendedShop()
    {
        var shop = CreateActiveShop();
        shop.Suspend("违规经营");
        return shop;
    }
}
