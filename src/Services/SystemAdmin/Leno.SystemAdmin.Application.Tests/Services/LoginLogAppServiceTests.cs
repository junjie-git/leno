using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class LoginLogAppServiceTests
{
    private readonly Mock<ILoginLogRepository> _repo = new();
    private readonly LoginLogAppService _service;

    public LoginLogAppServiceTests()
    {
        _service = new LoginLogAppService(_repo.Object, NullLogger<LoginLogAppService>.Instance);
    }

    [Fact]
    public async Task QueryAsync_WithFilters_PassesQueryToRepo()
    {
        var query = new LoginLogQuery { Username = "admin", Page = 1, PageSize = 20 };
        var logs = new List<LoginLog>
        {
            CreateLoginLog("admin")
        };
        _repo.Setup(r => r.QueryAsync(It.IsAny<LoginLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 1));

        var result = await _service.QueryAsync(query, default);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        _repo.Verify(r => r.QueryAsync(It.Is<LoginLogQuery>(q => q.Username == "admin"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_Pagination_ReturnsCorrectPage()
    {
        var query = new LoginLogQuery { Page = 2, PageSize = 10 };
        _repo.Setup(r => r.QueryAsync(It.IsAny<LoginLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LoginLog>(), 25));

        var result = await _service.QueryAsync(query, default);

        result.Total.Should().Be(25);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((LoginLog?)null);

        var result = await _service.GetByIdAsync(id, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExportAsync_BuildsCsvWithHeader()
    {
        var logs = new List<LoginLog> { CreateLoginLog("admin") };
        _repo.Setup(r => r.StreamAsync(It.IsAny<LoginLogQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(logs));

        var csv = await _service.ExportAsync(new LoginLogQuery(), default);

        csv.Should().NotBeNullOrEmpty();
        var firstLine = csv.Split('\n')[0];
        firstLine.Should().Contain("username");
        firstLine.Should().Contain("ipAddress");
        firstLine.Should().Contain("loginAt");
    }

    [Fact]
    public async Task ExportAsync_StreamLimit_StopsAt100000()
    {
        var manyLogs = Enumerable.Range(0, 100_001)
            .Select(_ => CreateLoginLog("u"))
            .ToList();
        _repo.Setup(r => r.StreamAsync(It.IsAny<LoginLogQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(manyLogs));

        var csv = await _service.ExportAsync(new LoginLogQuery(), default);

        var dataLines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1);
        dataLines.Should().HaveCount(100_000);
    }

    private static LoginLog CreateLoginLog(string username) =>
        LoginLog.CreateSuccess(
            Guid.NewGuid(),
            username,
            Guid.NewGuid(),
            "127.0.0.1",
            "Chrome",
            "Windows",
            "UA",
            "trace-1",
            100,
            DateTime.UtcNow);

    private static async IAsyncEnumerable<LoginLog> CreateAsyncEnumerable(List<LoginLog> logs)
    {
        foreach (var log in logs)
        {
            yield return log;
            await Task.Yield();
        }
    }
}
