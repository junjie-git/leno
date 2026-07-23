using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Leno.Infrastructure.Tests.Persistence;

/// <summary>
/// BaseDbContext 审计字段填充验证：CreatedBy/UpdatedBy 应由 ICurrentUserContext 解析。
/// 验证 P0-T7：FillAuditableFields 注入当前用户标识，消除审计追踪断裂。
/// </summary>
public class BaseDbContextAuditTests
{
    [Fact]
    public async Task SaveChangesAsync_OnAdd_ShouldFillCreatedByAndUpdatedBy()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns(userId);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase("audit-test-add")
            .Options;

        await using var context = new TestAuditDbContext(options, userContext.Object);
        var entity = new TestAuditableEntity { Name = "test" };

        // Act
        context.AuditableEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entity.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entity.CreatedBy.Should().Be(userId.ToString());
        entity.UpdatedBy.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task SaveChangesAsync_OnModify_ShouldFillUpdatedByOnly()
    {
        // Arrange — 首次以 creator 身份创建实体，CreatedBy 应为 creator
        var creatorId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns(creatorId);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase("audit-test-modify")
            .Options;

        await using var context = new TestAuditDbContext(options, userContext.Object);
        var entity = new TestAuditableEntity { Name = "original" };
        context.AuditableEntities.Add(entity);
        await context.SaveChangesAsync(); // Added: CreatedBy = creatorId, UpdatedBy = creatorId

        // Act — 切换到 modifier 身份修改实体
        userContext.SetupGet(x => x.UserId).Returns(modifierId);
        entity.Name = "modified";
        await context.SaveChangesAsync(); // Modified: UpdatedBy = modifierId, CreatedBy 不变

        // Assert
        entity.CreatedBy.Should().Be(creatorId.ToString(), "CreatedBy 在修改时不应被覆盖");
        entity.UpdatedBy.Should().Be(modifierId.ToString(), "UpdatedBy 应为当前修改者");
    }

    [Fact]
    public async Task SaveChangesAsync_AnonymousUser_ShouldFillSystemIdentifier()
    {
        // Arrange — 未认证用户（如后台任务）
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns((Guid?)null);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(false);

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase("audit-test-anonymous")
            .Options;

        await using var context = new TestAuditDbContext(options, userContext.Object);
        var entity = new TestAuditableEntity { Name = "bg-task" };

        // Act
        context.AuditableEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert
        entity.CreatedBy.Should().Be("system", "未认证用户的审计标识应为 system");
        entity.UpdatedBy.Should().Be("system");
    }

    [Fact]
    public async Task SaveChangesAsync_NullUserContext_ShouldFillSystemIdentifier()
    {
        // Arrange — CurrentUserContext 为 null（如后台迁移工具，未注入 ICurrentUserContext）
        var options = new DbContextOptionsBuilder<TestAuditDbContextNoUserContext>()
            .UseInMemoryDatabase("audit-test-null-context")
            .Options;

        await using var context = new TestAuditDbContextNoUserContext(options);
        var entity = new TestAuditableEntity { Name = "migration" };

        // Act
        context.AuditableEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert
        entity.CreatedBy.Should().Be("system", "无用户上下文时审计标识应为 system");
        entity.UpdatedBy.Should().Be("system");
    }

    /// <summary>
    /// 测试用 DbContext，注入 ICurrentUserContext 覆盖基类虚属性。
    /// </summary>
    private sealed class TestAuditDbContext : BaseDbContext
    {
        private readonly ICurrentUserContext _userContext;

        public DbSet<TestAuditableEntity> AuditableEntities => Set<TestAuditableEntity>();

        public TestAuditDbContext(DbContextOptions options, ICurrentUserContext userContext)
            : base(options)
        {
            _userContext = userContext;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase("fallback");
            }
            optionsBuilder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            // 调用基类 OnConfiguring 以注册 AuditableEntityInterceptor
            base.OnConfiguring(optionsBuilder);
        }

        protected override ICurrentUserContext? CurrentUserContext => _userContext;
    }

    /// <summary>
    /// 测试用 DbContext，不覆盖 CurrentUserContext（默认 null），验证 "system" 回退。
    /// </summary>
    private sealed class TestAuditDbContextNoUserContext : BaseDbContext
    {
        public DbSet<TestAuditableEntity> AuditableEntities => Set<TestAuditableEntity>();

        public TestAuditDbContextNoUserContext(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase("fallback-no-context");
            }
            optionsBuilder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            // 调用基类 OnConfiguring 以注册 AuditableEntityInterceptor
            base.OnConfiguring(optionsBuilder);
        }
    }

    /// <summary>
    /// 测试用审计实体，继承 Entity（已实现 IAuditable），仅添加 Name 属性。
    /// </summary>
    private sealed class TestAuditableEntity : Entity
    {
        public string Name { get; set; } = string.Empty;
    }
}
