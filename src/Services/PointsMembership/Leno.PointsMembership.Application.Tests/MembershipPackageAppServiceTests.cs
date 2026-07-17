using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;
using MembershipPackageAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipPackage;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 会员套餐应用服务单元测试，覆盖套餐查询、运营端 CRUD、启停与用户订阅场景。
/// </summary>
public class MembershipPackageAppServiceTests
{
    private readonly Mock<IMembershipPackageRepository> _packageRepoMock = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly MembershipPackageAppService _sut;

    private static readonly Guid PackageId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public MembershipPackageAppServiceTests()
    {
        _sut = new MembershipPackageAppService(
            _packageRepoMock.Object,
            _userMembershipRepoMock.Object,
            _uowMock.Object);
    }

    [Fact]
    public async Task GetPackagesAsync_NoPackages_ShouldReturnEmptyList()
    {
        _packageRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipPackageAggregate>());

        var result = await _sut.GetPackagesAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePackageAsync_Valid_ShouldAddAndSave()
    {
        var dto = new CreateMembershipPackageDto
        {
            Name = "黄金月卡",
            Level = 2,
            Price = 30m,
            DurationDays = 30,
            Benefits = "{\"discount\":0.9}"
        };

        var result = await _sut.CreatePackageAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("黄金月卡");
        result.Level.Should().Be(2);
        result.Price.Should().Be(30m);
        result.DurationDays.Should().Be(30);
        result.Status.Should().Be(PackageStatus.Enabled);
        _packageRepoMock.Verify(r => r.AddAsync(It.IsAny<MembershipPackageAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePackageAsync_NotExist_ShouldThrow()
    {
        _packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPackageAggregate?)null);
        var dto = new UpdateMembershipPackageDto
        {
            Name = "白银月卡",
            Price = 20m,
            DurationDays = 30,
            Benefits = "{\"discount\":0.95}"
        };

        var act = () => _sut.UpdatePackageAsync(PackageId, dto);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePackageAsync_Valid_ShouldUpdateAndSave()
    {
        var package = MembershipPackageAggregate.Create(
            PackageId, "黄金月卡", 2, 30m, 30, "{\"discount\":0.9}");
        _packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        var dto = new UpdateMembershipPackageDto
        {
            Name = "白银月卡",
            Price = 20m,
            DurationDays = 30,
            Benefits = "{\"discount\":0.95}"
        };

        var result = await _sut.UpdatePackageAsync(PackageId, dto);

        result.Name.Should().Be("白银月卡");
        result.Price.Should().Be(20m);
        result.Benefits.Should().Be("{\"discount\":0.95}");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnablePackageAsync_AlreadyEnabled_ShouldThrow()
    {
        var package = MembershipPackageAggregate.Create(
            PackageId, "黄金月卡", 2, 30m, 30, "{\"discount\":0.9}");
        _packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var act = () => _sut.EnablePackageAsync(PackageId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*已启用*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisablePackageAsync_Valid_ShouldDisableAndSave()
    {
        var package = MembershipPackageAggregate.Create(
            PackageId, "黄金月卡", 2, 30m, 30, "{\"discount\":0.9}");
        _packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        await _sut.DisablePackageAsync(PackageId);

        package.Status.Should().Be(PackageStatus.Disabled);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_PackageNotExist_ShouldThrow()
    {
        _packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPackageAggregate?)null);

        var act = () => _sut.SubscribeAsync(UserId, PackageId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*不存在*");
        _userMembershipRepoMock.Verify(r => r.AddAsync(It.IsAny<UserMembership>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubscribeAsync_Valid_ShouldAddUserMembershipAndSave()
    {
        var package = MembershipPackageAggregate.Create(
            PackageId, "黄金月卡", 2, 30m, 30, "{\"discount\":0.9}");
        _packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        await _sut.SubscribeAsync(UserId, PackageId);

        _userMembershipRepoMock.Verify(
            r => r.AddAsync(It.Is<UserMembership>(u => u.UserId == UserId && u.PackageId == PackageId && u.Level == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
