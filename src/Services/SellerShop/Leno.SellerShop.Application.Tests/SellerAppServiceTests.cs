using FluentValidation;
using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.Repositories;
using Leno.SellerShop.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.SellerShop.Application.Tests;

/// <summary>
/// 卖家档案应用服务单元测试，覆盖档案提交、更新、审核通过/驳回用例。
/// </summary>
public class SellerAppServiceTests
{
    private readonly Mock<ISellerProfileRepository> _profileRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IValidator<SubmitSellerProfileDto>> _submitValidatorMock = new();
    private readonly Mock<IValidator<ActionReasonDto>> _actionReasonValidatorMock = new();
    private readonly SellerAppService _sut;

    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();

    public SellerAppServiceTests()
    {
        _submitValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SubmitSellerProfileDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _actionReasonValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ActionReasonDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _sut = new SellerAppService(
            _profileRepoMock.Object,
            _uowMock.Object,
            _submitValidatorMock.Object,
            _actionReasonValidatorMock.Object);
    }

    [Fact]
    public async Task SubmitSellerProfileAsync_NewProfile_ShouldCreateAndSave()
    {
        var dto = BuildSubmitDto();
        _profileRepoMock
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SellerProfile?)null);

        var result = await _sut.SubmitSellerProfileAsync(UserId, dto);

        result.UserId.Should().Be(UserId);
        result.RealName.Should().Be("张三");
        result.Status.Should().Be(SellerStatus.PendingReview);
        _profileRepoMock.Verify(r => r.AddAsync(It.IsAny<SellerProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitSellerProfileAsync_ExistingProfile_ShouldUpdateAndSave()
    {
        var dto = BuildSubmitDto();
        var existing = SellerProfile.Create(ProfileId, UserId, "李四");
        _profileRepoMock
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.SubmitSellerProfileAsync(UserId, dto);

        result.RealName.Should().Be("张三");
        _profileRepoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _profileRepoMock.Verify(r => r.AddAsync(It.IsAny<SellerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSellerProfileAsync_Existing_ShouldReturnDto()
    {
        var profile = SellerProfile.Create(ProfileId, UserId, "张三");
        _profileRepoMock
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _sut.GetSellerProfileAsync(UserId);

        result.Id.Should().Be(ProfileId);
        result.UserId.Should().Be(UserId);
        result.RealName.Should().Be("张三");
    }

    [Fact]
    public async Task GetSellerProfileAsync_NotFound_ShouldThrowDomainException()
    {
        _profileRepoMock
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SellerProfile?)null);

        var act = () => _sut.GetSellerProfileAsync(UserId);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*卖家档案不存在*");
    }

    [Fact]
    public async Task ApproveSellerProfileAsync_Existing_ShouldApproveAndSave()
    {
        var profile = SellerProfile.Create(ProfileId, UserId, "张三", idCard: "110101199001011234");
        profile.SubmitForVerification();
        _profileRepoMock
            .Setup(r => r.GetByIdAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        await _sut.ApproveSellerProfileAsync(ProfileId, ReviewerId);

        profile.Status.Should().Be(SellerStatus.Approved);
        profile.ReviewedBy.Should().Be(ReviewerId);
        _profileRepoMock.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectSellerProfileAsync_Existing_ShouldRejectWithReason()
    {
        var profile = SellerProfile.Create(ProfileId, UserId, "张三", idCard: "110101199001011234");
        profile.SubmitForVerification();
        _profileRepoMock
            .Setup(r => r.GetByIdAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        await _sut.RejectSellerProfileAsync(ProfileId, ReviewerId, new ActionReasonDto { Reason = "资质不完整" });

        profile.Status.Should().Be(SellerStatus.Rejected);
        profile.StatusReason.Should().Be("资质不完整");
        profile.ReviewedBy.Should().Be(ReviewerId);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitSellerProfileAsync_EmptyUserId_ShouldThrowDomainException()
    {
        var dto = BuildSubmitDto();

        var act = () => _sut.SubmitSellerProfileAsync(Guid.Empty, dto);

        await act.Should().ThrowAsync<SellerShopDomainException>().WithMessage("*卖家账号标识不可为空*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SubmitSellerProfileDto BuildSubmitDto() => new()
    {
        RealName = "张三",
        IdCard = "110101199001011234",
        BusinessLicenseNo = null,
        BankAccount = "6222021234567890123"
    };
}
