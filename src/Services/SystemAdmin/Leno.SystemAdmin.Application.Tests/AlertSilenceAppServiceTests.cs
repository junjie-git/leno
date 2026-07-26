using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Application.Tests;

/// <summary>
/// 告警静默规则应用服务单元测试，覆盖创建、查询、删除用例与匹配器/时长/原因校验。
/// </summary>
public class AlertSilenceAppServiceTests
{
    private readonly Mock<IAlertmanagerClient> _clientMock = new();
    private readonly AlertSilenceAppService _sut;

    private static readonly Guid SilenceId = Guid.NewGuid();
    private const string OperatorId = "op-001";

    public AlertSilenceAppServiceTests()
    {
        _sut = new AlertSilenceAppService(_clientMock.Object, NullLogger<AlertSilenceAppService>.Instance);
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_Valid_ShouldReturnSilenceDto()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = new List<MatcherItemDto>
            {
                new() { Name = "module", Value = "Payment", IsRegex = false }
            },
            Duration = "2h",
            Reason = "维护期间静默"
        };

        var silence = CreateSilence();
        _clientMock
            .Setup(c => c.CreateSilenceAsync(It.IsAny<string>(), dto.Duration, dto.Reason, OperatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(silence);

        var result = await _sut.CreateAsync(dto, OperatorId);

        result.Should().NotBeNull();
        result.SilenceId.Should().Be(silence.Id);
        result.Duration.Should().Be("2h");
        result.Reason.Should().Be("维护期间静默");
        result.CreatedBy.Should().Be(OperatorId);
        result.Matchers.Should().Contain("module").And.Contain("Payment");
        result.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_NullDto_ShouldThrow()
    {
        var act = () => _sut.CreateAsync(null!, OperatorId);

        await act.Should().ThrowAsync<ArgumentNullException>();
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmptyCreatedBy_ShouldThrow()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = new List<MatcherItemDto> { new() { Name = "module", Value = "Payment" } },
            Duration = "2h",
            Reason = "维护"
        };

        var act = () => _sut.CreateAsync(dto, "");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*创建人标识不可为空*");
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmptyMatchers_ShouldThrow()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = new List<MatcherItemDto>(),
            Duration = "2h",
            Reason = "维护"
        };

        var act = () => _sut.CreateAsync(dto, OperatorId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*匹配器不可为空*");
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_TooManyMatchers_ShouldThrow()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = Enumerable.Range(0, 33)
                .Select(i => new MatcherItemDto { Name = $"n{i}", Value = $"v{i}" })
                .ToList(),
            Duration = "2h",
            Reason = "维护"
        };

        var act = () => _sut.CreateAsync(dto, OperatorId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*匹配器数量不可超过 32*");
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_MatcherWithEmptyName_ShouldThrow()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = new List<MatcherItemDto> { new() { Name = "", Value = "Payment" } },
            Duration = "2h",
            Reason = "维护"
        };

        var act = () => _sut.CreateAsync(dto, OperatorId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*匹配器名称不可为空*");
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_MatcherWithEmptyValue_ShouldThrow()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = new List<MatcherItemDto> { new() { Name = "module", Value = "" } },
            Duration = "2h",
            Reason = "维护"
        };

        var act = () => _sut.CreateAsync(dto, OperatorId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*匹配器值不可为空*");
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmptyDuration_ShouldThrow()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = new List<MatcherItemDto> { new() { Name = "module", Value = "Payment" } },
            Duration = "",
            Reason = "维护"
        };

        var act = () => _sut.CreateAsync(dto, OperatorId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*持续时长不可为空*");
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmptyReason_ShouldThrow()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = new List<MatcherItemDto> { new() { Name = "module", Value = "Payment" } },
            Duration = "2h",
            Reason = ""
        };

        var act = () => _sut.CreateAsync(dto, OperatorId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*静默原因不可为空*");
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_TooLongReason_ShouldThrow()
    {
        var dto = new CreateAlertSilenceDto
        {
            Matchers = new List<MatcherItemDto> { new() { Name = "module", Value = "Payment" } },
            Duration = "2h",
            Reason = new string('r', 1001)
        };

        var act = () => _sut.CreateAsync(dto, OperatorId);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*静默原因长度不可超过 1000*");
        _clientMock.Verify(c => c.CreateSilenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region QueryAsync

    [Fact]
    public async Task QueryAsync_ShouldReturnSilenceList()
    {
        var silences = new List<AlertSilence> { CreateSilence(), CreateSilence() };
        _clientMock
            .Setup(c => c.GetSilencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(silences);

        var result = await _sut.QueryAsync();

        result.Items.Should().HaveCount(2);
        result.Items[0].SilenceId.Should().Be(silences[0].Id);
        result.Items[1].SilenceId.Should().Be(silences[1].Id);
    }

    [Fact]
    public async Task QueryAsync_EmptyResult_ShouldReturnEmptyList()
    {
        _clientMock
            .Setup(c => c.GetSilencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertSilence>());

        var result = await _sut.QueryAsync();

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_ExpiredSilence_ShouldMarkIsExpiredTrue()
    {
        var expiredSilence = AlertSilence.Create(
            Guid.NewGuid(),
            "[{\"name\":\"module\",\"value\":\"Payment\",\"isRegex\":false}]",
            "1h",
            "已过期",
            DateTime.UtcNow.AddHours(-3),
            DateTime.UtcNow.AddHours(-1),
            OperatorId,
            DateTime.UtcNow.AddHours(-3));

        _clientMock
            .Setup(c => c.GetSilencesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertSilence> { expiredSilence });

        var result = await _sut.QueryAsync();

        result.Items.Should().HaveCount(1);
        result.Items[0].IsExpired.Should().BeTrue();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ShouldDelegateToClient()
    {
        await _sut.DeleteAsync(SilenceId);

        _clientMock.Verify(c => c.DeleteSilenceAsync(SilenceId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyId_ShouldThrow()
    {
        var act = () => _sut.DeleteAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*静默规则标识不可为空*");
        _clientMock.Verify(c => c.DeleteSilenceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    private static AlertSilence CreateSilence()
        => AlertSilence.Create(
            SilenceId,
            "[{\"name\":\"module\",\"value\":\"Payment\",\"isRegex\":false}]",
            "2h",
            "维护期间静默",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            OperatorId,
            DateTime.UtcNow);
}
