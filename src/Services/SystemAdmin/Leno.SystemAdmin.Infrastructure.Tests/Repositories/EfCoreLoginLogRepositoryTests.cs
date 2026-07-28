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
        CreateLoginLogsTable();
        _repo = new EfCoreLoginLogRepository(_db);
    }

    /// <summary>
    /// 手动创建 login_logs 表（SQLite 兼容）。
    /// 不使用 EnsureCreated()，因为 SystemAdminDbContext 包含其他实体配置使用了 nvarchar(max)，
    /// 该列类型在 SQLite 中会导致语法错误。LoginLog 配置仅使用 HasMaxLength，SQLite 兼容。
    /// </summary>
    private void CreateLoginLogsTable()
    {
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS login_logs (
                id TEXT NOT NULL PRIMARY KEY,
                username TEXT NOT NULL,
                user_id TEXT,
                ip_address TEXT NOT NULL,
                geo_location TEXT,
                browser TEXT NOT NULL,
                os TEXT NOT NULL,
                result INTEGER NOT NULL,
                failure_reason TEXT,
                duration_ms INTEGER NOT NULL,
                user_agent TEXT NOT NULL,
                device_fingerprint TEXT,
                referer_url TEXT,
                trace_id TEXT NOT NULL,
                event_id TEXT,
                login_at TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                created_by TEXT,
                updated_by TEXT,
                version BLOB
            );
            CREATE INDEX IF NOT EXISTS ix_login_logs_login_at ON login_logs (login_at DESC);
            CREATE INDEX IF NOT EXISTS ix_login_logs_username_login_at ON login_logs (username, login_at DESC);
            CREATE INDEX IF NOT EXISTS ix_login_logs_result_login_at ON login_logs (result, login_at DESC);
            CREATE INDEX IF NOT EXISTS ix_login_logs_event_id ON login_logs (event_id);
            """);
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
