using FluentValidation;
using FluentValidation.Results;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.UserAuth.Application.Tests;

public class AddressAppServiceTests
{
    private readonly Mock<IAddressRepository> _addressRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IValidator<SaveAddressDto>> _validatorMock = new();
    private readonly AddressAppService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public AddressAppServiceTests()
    {
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<SaveAddressDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _sut = new AddressAppService(
            _addressRepoMock.Object,
            _userRepoMock.Object,
            _uowMock.Object,
            _validatorMock.Object);
    }

    private static Address CreateAddress(Guid userId, Guid? addressId = null, bool isDefault = false)
    {
        return Address.Create(
            addressId ?? Guid.NewGuid(), userId, "Zhang San", "+8613800138000",
            "Zhejiang", "Hangzhou", "Xihu",
            "杭州市西湖区文一路123号5栋301室",
            "Home", isDefault);
    }

    private static User CreateUser(Guid userId)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hashed:{p}");
        return User.Create(
            userId, "testuser", "test@test.com", null,
            hasher.Object.Hash("Pass123!"), "Test Nick", null);
    }

    private static SaveAddressDto CreateSaveAddressDto(bool isDefault = false)
    {
        return new SaveAddressDto
        {
            RecipientName = "Zhang San",
            RecipientPhone = "+8613800138000",
            Province = "Zhejiang",
            City = "Hangzhou",
            District = "Xihu",
            Detail = "杭州市西湖区文一路123号5栋301室",
            Tag = "Home",
            IsDefault = isDefault
        };
    }

    #region ListAsync

    [Fact]
    public async Task ListAsync_WithAddresses_ShouldReturnOrderedList()
    {
        var defaultAddr = CreateAddress(_userId, isDefault: true);
        var normalAddr = CreateAddress(_userId, isDefault: false);
        _addressRepoMock.Setup(r => r.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Address> { normalAddr, defaultAddr });

        var result = await _sut.ListAsync(_userId);

        result.Should().HaveCount(2);
        result[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task ListAsync_Empty_ShouldReturnEmptyList()
    {
        _addressRepoMock.Setup(r => r.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Address>());

        var result = await _sut.ListAsync(_userId);

        result.Should().BeEmpty();
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidInput_ShouldReturnAddressDto()
    {
        _addressRepoMock.Setup(r => r.CountActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _addressRepoMock.Setup(r => r.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Address>());
        _userRepoMock.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(_userId));

        var dto = CreateSaveAddressDto();

        var result = await _sut.CreateAsync(_userId, dto);

        result.Should().NotBeNull();
        result.RecipientName.Should().Be("Zhang San");
        result.IsDefault.Should().BeTrue(); // first address auto-default
        _addressRepoMock.Verify(r => r.AddAsync(It.IsAny<Address>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_LimitExceeded_ShouldThrowDomainException()
    {
        _addressRepoMock.Setup(r => r.CountActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AddressAppService.MaxAddressesPerUser);

        var dto = CreateSaveAddressDto();

        var act = () => _sut.CreateAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*地址*");
    }

    [Fact]
    public async Task CreateAsync_ValidationFailure_ShouldThrowValidationException()
    {
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<SaveAddressDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("RecipientName", "收件人不可为空") }));

        var dto = new SaveAddressDto();

        var act = () => _sut.CreateAsync(_userId, dto);

        await act.Should().ThrowAsync<UserAuthValidationException>();
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ValidInput_ShouldUpdateAndReturnDto()
    {
        var address = CreateAddress(_userId);
        _addressRepoMock.Setup(r => r.GetByIdAsync(address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var dto = new SaveAddressDto
        {
            RecipientName = "Li Si",
            RecipientPhone = "+8613900139000",
            Province = "Beijing",
            City = "Beijing",
            District = "Chaoyang",
            Detail = "朝阳区建国路88号SOHO现代城",
            Tag = "Work"
        };

        var result = await _sut.UpdateAsync(_userId, address.Id, dto);

        result.RecipientName.Should().Be("Li Si");
        result.Tag.Should().Be("Work");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NotOwned_ShouldThrowDomainException()
    {
        var address = CreateAddress(Guid.NewGuid()); // different userId
        _addressRepoMock.Setup(r => r.GetByIdAsync(address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var dto = CreateSaveAddressDto();

        var act = () => _sut.UpdateAsync(_userId, address.Id, dto);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*无权*");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ShouldThrowDomainException()
    {
        _addressRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);

        var dto = CreateSaveAddressDto();

        var act = () => _sut.UpdateAsync(_userId, Guid.NewGuid(), dto);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*地址不存在*");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ValidAddress_ShouldSoftDelete()
    {
        var address = CreateAddress(_userId);
        _addressRepoMock.Setup(r => r.GetByIdAsync(address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        await _sut.DeleteAsync(_userId, address.Id);

        address.Status.Should().Be(AddressStatus.Deleted);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DefaultAddress_ShouldAutoSelectNext()
    {
        var address = CreateAddress(_userId, isDefault: true);
        _addressRepoMock.Setup(r => r.GetByIdAsync(address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var nextAddress = CreateAddress(_userId, isDefault: false);
        _addressRepoMock.Setup(r => r.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Address> { nextAddress });
        _userRepoMock.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(_userId));

        await _sut.DeleteAsync(_userId, address.Id);

        nextAddress.IsDefault.Should().BeTrue();
    }

    #endregion

    #region SetDefaultAsync

    [Fact]
    public async Task SetDefaultAsync_ValidAddress_ShouldSetDefault()
    {
        var address = CreateAddress(_userId, isDefault: false);
        _addressRepoMock.Setup(r => r.GetByIdAsync(address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _addressRepoMock.Setup(r => r.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Address>());
        _userRepoMock.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(_userId));

        var result = await _sut.SetDefaultAsync(_userId, address.Id);

        result.IsDefault.Should().BeTrue();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}