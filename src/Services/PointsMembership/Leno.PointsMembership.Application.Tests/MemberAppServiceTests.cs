using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;
using MembershipLevelAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipLevel;
using MemberAggregate = Leno.PointsMembership.Domain.Aggregates.Member;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 会员管理应用服务单元测试，覆盖会员信息查询与运营端等级 CRUD、启停用例。
/// </summary>
public class MemberAppServiceTests
{
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IMembershipLevelRepository> _levelRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly MemberAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid LevelId = Guid.NewGuid();

    public MemberAppServiceTests()
    {
        _sut = new MemberAppService(_memberRepoMock.Object, _levelRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task GetMemberInfoAsync_Existing_ShouldReturnDto()
    {
        var member = MemberAggregate.Create(MemberId, UserId);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _sut.GetMemberInfoAsync(UserId);

        result.Should().NotBeNull();
        result.UserId.Should().Be(UserId);
        result.CurrentLevel.Should().Be(1);
        result.Status.Should().Be(MemberStatus.Active);
    }

    [Fact]
    public async Task GetMemberInfoAsync_NotExist_ShouldThrowNotFoundException()
    {
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberAggregate?)null);

        var act = () => _sut.GetMemberInfoAsync(UserId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*会员档案不存在*");
    }

    [Fact]
    public async Task CreateLevelAsync_Valid_ShouldAddAndSave()
    {
        var dto = new CreateMembershipLevelDto
        {
            Name = "黄金会员",
            Level = 2,
            MinConsumption = 1000m,
            DiscountRate = 0.9m
        };

        var result = await _sut.CreateLevelAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("黄金会员");
        result.Level.Should().Be(2);
        result.Status.Should().Be(MembershipLevelStatus.Enabled);
        _levelRepoMock.Verify(r => r.AddAsync(It.IsAny<MembershipLevelAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLevelAsync_NotExist_ShouldThrowNotFoundException()
    {
        _levelRepoMock.Setup(r => r.GetByIdAsync(LevelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipLevelAggregate?)null);
        var dto = new UpdateMembershipLevelDto
        {
            Name = "白银会员",
            MinConsumption = 500m,
            DiscountRate = 0.95m
        };

        var act = () => _sut.UpdateLevelAsync(LevelId, dto);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*不存在*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateLevelAsync_Valid_ShouldUpdateAndSave()
    {
        var level = MembershipLevelAggregate.Create(LevelId, "白银会员", 2, 500m, 0.95m);
        _levelRepoMock.Setup(r => r.GetByIdAsync(LevelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);
        var dto = new UpdateMembershipLevelDto
        {
            Name = "黄金会员",
            MinConsumption = 1000m,
            DiscountRate = 0.9m
        };

        var result = await _sut.UpdateLevelAsync(LevelId, dto);

        result.Name.Should().Be("黄金会员");
        result.MinConsumption.Should().Be(1000m);
        result.DiscountRate.Should().Be(0.9m);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableLevelAsync_AlreadyEnabled_ShouldThrow()
    {
        var level = MembershipLevelAggregate.Create(LevelId, "黄金会员", 2, 1000m, 0.9m);
        _levelRepoMock.Setup(r => r.GetByIdAsync(LevelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);

        var act = () => _sut.EnableLevelAsync(LevelId);

        await act.Should().ThrowAsync<PointsDomainException>()
            .WithMessage("*已启用*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisableLevelAsync_Valid_ShouldDisableAndSave()
    {
        var level = MembershipLevelAggregate.Create(LevelId, "黄金会员", 2, 1000m, 0.9m);
        _levelRepoMock.Setup(r => r.GetByIdAsync(LevelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);

        await _sut.DisableLevelAsync(LevelId);

        level.Status.Should().Be(MembershipLevelStatus.Disabled);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLevelsAsync_ShouldReturnAllLevels()
    {
        var levels = new List<MembershipLevelAggregate>
        {
            MembershipLevelAggregate.Create(Guid.NewGuid(), "普通会员", 1, 0m, 1m),
            MembershipLevelAggregate.Create(Guid.NewGuid(), "黄金会员", 2, 1000m, 0.9m)
        };
        _levelRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(levels);

        var result = await _sut.GetLevelsAsync();

        result.Should().HaveCount(2);
        result[0].Level.Should().Be(1);
        result[1].Level.Should().Be(2);
    }
}
