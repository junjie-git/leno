using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure;
using Leno.SystemAdmin.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Tests.Repositories;

public sealed class EfCoreLoginLogRepositoryTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SystemAdminDbContext _db;
    private readonly EfCoreLoginLogRepository _repo;

    public EfCoreLoginLogRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SystemAdminDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SystemAdminDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new EfCoreLoginLogRepository(_db);
    }

    [Fact]
    public async Task AddAsync_PersistsLog()
    {
        var log = LoginLog.CreateSuccess(Guid.NewGuid(), "admin", Guid.NewGuid(), "10.0.0.1",
            "Chrome 120", "Windows 11", "Mozilla/5.0", "trace-1", 100, DateTime.UtcNow);

        await _repo.AddAsync(log, default);

        var loaded = await _repo.GetByIdAsync(log.Id, default);
        loaded.Should().NotBeNull();
        loaded!.Username.Should().Be("admin");
    }

    [Fact]
    public async Task QueryAsync_ByUsername_FiltersCorrectly()
    {
        await _repo.AddAsync(LoginLog.CreateSuccess(Guid.NewGuid(), "admin", Guid.NewGuid(), "1.1.1.1",
            "Chrome", "Windows", "UA", "t1", 50, DateTime.UtcNow), default);
        await _repo.AddAsync(LoginLog.CreateSuccess(Guid.NewGuid(), "operator", Guid.NewGuid(), "1.1.1.2",
            "Chrome", "Windows", "UA", "t2", 50, DateTime.UtcNow), default);

        var (items, total) = await _repo.QueryAsync(new LoginLogQuery { Username = "admin" }, default);

        total.Should().Be(1);
        items.Should().HaveCount(1);
        items[0].Username.Should().Be("admin");
    }

    [Fact]
    public async Task QueryAsync_ByResult_FiltersSuccessOnly()
    {
        await _repo.AddAsync(LoginLog.CreateSuccess(Guid.NewGuid(), "u1", Guid.NewGuid(), "1.1.1.1",
            "Chrome", "Windows", "UA", "t1", 50, DateTime.UtcNow), default);
        await _repo.AddAsync(LoginLog.CreateFailed(Guid.NewGuid(), "u1", "1.1.1.1",
            "Chrome", "Windows", "UA", "t2", 50, "密码错误", DateTime.UtcNow), default);

        var (items, total) = await _repo.QueryAsync(new LoginLogQuery { Result = LoginResult.Success }, default);

        total.Should().Be(1);
        items.Should().HaveCount(1);
        items[0].Result.Should().Be(LoginResult.Success);
    }

    [Fact]
    public async Task QueryAsync_ByTimeRange_FiltersByLoginAt()
    {
        var now = DateTime.UtcNow;
        await _repo.AddAsync(LoginLog.CreateSuccess(Guid.NewGuid(), "u1", Guid.NewGuid(), "1.1.1.1",
            "Chrome", "Windows", "UA", "t1", 50, now.AddHours(-3)), default);
        await _repo.AddAsync(LoginLog.CreateSuccess(Guid.NewGuid(), "u2", Guid.NewGuid(), "1.1.1.2",
            "Chrome", "Windows", "UA", "t2", 50, now), default);

        var (items, total) = await _repo.QueryAsync(new LoginLogQuery
        {
            LoginAtFrom = now.AddHours(-1),
            LoginAtTo = now.AddMinutes(1)
        }, default);

        total.Should().Be(1);
        items[0].Username.Should().Be("u2");
    }

    [Fact]
    public async Task QueryAsync_Pagination_ReturnsCorrectPage()
    {
        for (int i = 0; i < 15; i++)
        {
            await _repo.AddAsync(LoginLog.CreateSuccess(Guid.NewGuid(), $"u{i}", Guid.NewGuid(), "1.1.1.1",
                "Chrome", "Windows", "UA", $"t{i}", 50, DateTime.UtcNow.AddSeconds(-i)), default);
        }

        var (items, total) = await _repo.QueryAsync(new LoginLogQuery { Page = 2, PageSize = 10 }, default);

        total.Should().Be(15);
        items.Should().HaveCount(5);
    }

    [Fact]
    public async Task StreamAsync_YieldsInDescendingOrder()
    {
        var now = DateTime.UtcNow;
        await _repo.AddAsync(LoginLog.CreateSuccess(Guid.NewGuid(), "older", Guid.NewGuid(), "1.1.1.1",
            "Chrome", "Windows", "UA", "t1", 50, now.AddHours(-1)), default);
        await _repo.AddAsync(LoginLog.CreateSuccess(Guid.NewGuid(), "newer", Guid.NewGuid(), "1.1.1.2",
            "Chrome", "Windows", "UA", "t2", 50, now), default);

        var result = new List<LoginLog>();
        await foreach (var log in _repo.StreamAsync(new LoginLogQuery(), 100, default))
        {
            result.Add(log);
        }

        result.Should().HaveCount(2);
        result[0].Username.Should().Be("newer");
        result[1].Username.Should().Be("older");
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
