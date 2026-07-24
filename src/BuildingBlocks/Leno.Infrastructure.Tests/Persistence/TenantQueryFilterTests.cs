using Leno.Infrastructure.Abstractions.MultiTenancy;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Leno.Infrastructure.Tests.Persistence;

/// <summary>
/// 多租户预留扩展位验证（4.7）。
/// <para>
/// 验证内容：
/// <list type="number">
/// <item>ITenantEntity 实体自动配置 tenant_id 列 + 全局查询过滤器（扩展位存在）。</item>
/// <item>TenantContext=null（默认单租户模式）时返回所有数据，默认行为不变。</item>
/// <item>TenantContext 设置当前租户时，仅返回全局数据 + 当前租户数据（多租户隔离）。</item>
/// <item>TenantQueryFilterInterceptor 在保存时自动填充 TenantId（仅当 CurrentTenantId 非 null 时）。</item>
/// </list>
/// </para>
/// </summary>
public class TenantQueryFilterTests
{
    [Fact]
    public void TenantEntity_ShouldHaveTenantIdColumnConfigured()
    {
        // Arrange — 构建 DbContext 模型
        var options = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseInMemoryDatabase("tenant-column-test_" + Guid.NewGuid())
            .Options;
        using var context = new TestTenantDbContext(options, new TenantContext());

        // Act
        var entityType = context.Model.FindEntityType(typeof(TestTenantEntity));

        // Assert — tenant_id 列已声明且 nullable
        entityType.Should().NotBeNull("TestTenantEntity 必须在 DbContext 模型中注册");
        var tenantIdProperty = entityType!.FindProperty(nameof(ITenantEntity.TenantId));
        tenantIdProperty.Should().NotBeNull("ITenantEntity 实体应自动配置 TenantId 属性");
        tenantIdProperty!.IsNullable.Should().BeTrue("当前阶段 tenant_id 应为 nullable（单租户模式，DG-7 通过后改 required）");
    }

    [Fact]
    public void TenantEntity_ShouldHaveQueryFilterApplied()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseInMemoryDatabase("tenant-filter-exists-test_" + Guid.NewGuid())
            .Options;
        using var context = new TestTenantDbContext(options, new TenantContext());

        // Act
        var entityType = context.Model.FindEntityType(typeof(TestTenantEntity));

        // Assert — 查询过滤器已应用
        entityType.Should().NotBeNull();
        entityType!.GetQueryFilter().Should().NotBeNull("ITenantEntity 实体应自动应用全局查询过滤器");
    }

    [Fact]
    public async Task QueryFilter_NullTenantContext_ReturnsAllData()
    {
        // Arrange — TenantContext 为 null（默认单租户模式，如后台迁移工具）
        var dbName = "tenant-null-context-test_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<TestTenantDbContextNoTenant>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // 插入不同租户的实体 + 全局实体（绕过查询过滤器直接写入）
        await using (var seedContext = new TestTenantDbContextNoTenant(options))
        {
            seedContext.TenantEntities.AddRange(
                new TestTenantEntity { Name = "global", TenantId = null },
                new TestTenantEntity { Name = "tenant-a", TenantId = tenantA },
                new TestTenantEntity { Name = "tenant-b", TenantId = tenantB });
            await seedContext.SaveChangesAsync();
        }

        // Act — 查询所有实体（TenantContext=null，应返回全部）
        await using var queryContext = new TestTenantDbContextNoTenant(options);
        var results = await queryContext.TenantEntities.ToListAsync();

        // Assert — 默认行为不变：返回所有数据
        results.Should().HaveCount(3, "TenantContext=null（单租户模式）时全局查询过滤器应返回所有数据");
        results.Select(e => e.Name).Should().Contain(new[] { "global", "tenant-a", "tenant-b" });
    }

    [Fact]
    public async Task QueryFilter_WithTenant_ReturnsOnlyMatchingData()
    {
        // Arrange — 设置当前租户
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = "tenant-filter-active-test_" + Guid.NewGuid();

        // 先用无租户上下文的 DbContext 写入测试数据
        var options = new DbContextOptionsBuilder<TestTenantDbContextNoTenant>()
            .UseInMemoryDatabase(dbName)
            .Options;
        await using (var seedContext = new TestTenantDbContextNoTenant(options))
        {
            seedContext.TenantEntities.AddRange(
                new TestTenantEntity { Name = "global", TenantId = null },
                new TestTenantEntity { Name = "tenant-a", TenantId = tenantA },
                new TestTenantEntity { Name = "tenant-b", TenantId = tenantB });
            await seedContext.SaveChangesAsync();
        }

        // Act — 用设置了租户 A 的 DbContext 查询
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantA);
        var queryOptions = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        await using var queryContext = new TestTenantDbContext(queryOptions, tenantContext);
        var results = await queryContext.TenantEntities.ToListAsync();

        // Assert — 多租户模式：仅返回全局数据 + 当前租户数据
        results.Should().HaveCount(2, "多租户模式应仅返回全局数据（TenantId=null）+ 当前租户数据");
        results.Select(e => e.Name).Should().Contain(new[] { "global", "tenant-a" });
        results.Should().NotContain(e => e.Name == "tenant-b", "其他租户数据应被全局查询过滤器排除");
    }

    [Fact]
    public async Task Interceptor_WithTenant_FillsTenantIdOnAdd()
    {
        // Arrange — 设置当前租户
        var tenantA = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantA);

        var options = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseInMemoryDatabase("tenant-interceptor-fill-test_" + Guid.NewGuid())
            .Options;
        await using var context = new TestTenantDbContext(options, tenantContext);

        // Act — 添加新实体（不手动设置 TenantId）
        var entity = new TestTenantEntity { Name = "auto-filled" };
        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert — 拦截器自动填充 TenantId
        entity.TenantId.Should().Be(tenantA, "TenantQueryFilterInterceptor 应在保存时自动填充当前租户 ID");
    }

    [Fact]
    public async Task Interceptor_NullTenantContext_KeepsTenantIdNull()
    {
        // Arrange — TenantContext 为 null（默认单租户模式，如后台迁移工具）
        var options = new DbContextOptionsBuilder<TestTenantDbContextNoTenant>()
            .UseInMemoryDatabase("tenant-interceptor-null-context-test_" + Guid.NewGuid())
            .Options;
        await using var context = new TestTenantDbContextNoTenant(options);

        // Act — 添加新实体
        var entity = new TestTenantEntity { Name = "no-tenant" };
        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert — 默认行为不变：TenantId 保持 null
        entity.TenantId.Should().BeNull("TenantContext=null 时拦截器不应填充 TenantId，默认行为不变");
    }

    [Fact]
    public async Task Interceptor_NullCurrentTenant_KeepsTenantIdNull()
    {
        // Arrange — TenantContext 存在但 CurrentTenantId 为 null（单租户模式默认状态）
        var tenantContext = new TenantContext(); // CurrentTenantId 默认 null

        var options = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseInMemoryDatabase("tenant-interceptor-null-current-test_" + Guid.NewGuid())
            .Options;
        await using var context = new TestTenantDbContext(options, tenantContext);

        // Act — 添加新实体
        var entity = new TestTenantEntity { Name = "null-current-tenant" };
        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert — 默认行为不变：TenantId 保持 null
        entity.TenantId.Should().BeNull("CurrentTenantId=null（单租户模式）时拦截器不应填充 TenantId");
    }

    [Fact]
    public async Task Interceptor_DoesNotOverrideExistingTenantId()
    {
        // Arrange — 设置当前租户 A，但实体已手动设置租户 B
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantA);

        var options = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseInMemoryDatabase("tenant-interceptor-preserve-test_" + Guid.NewGuid())
            .Options;
        await using var context = new TestTenantDbContext(options, tenantContext);

        // Act — 添加已手动设置 TenantId 的实体
        var entity = new TestTenantEntity { Name = "manual-tenant", TenantId = tenantB };
        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert — 拦截器不覆盖已设置的 TenantId
        entity.TenantId.Should().Be(tenantB, "拦截器不应覆盖已手动设置的 TenantId");
    }

    /// <summary>
    /// 测试用 DbContext，注入 <see cref="ITenantContext"/> 覆盖基类虚属性。
    /// </summary>
    private sealed class TestTenantDbContext : BaseDbContext
    {
        private readonly ITenantContext _tenantContext;

        public DbSet<TestTenantEntity> TenantEntities => Set<TestTenantEntity>();

        public TestTenantDbContext(DbContextOptions options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase("fallback-tenant");
            }
            optionsBuilder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            // 调用基类 OnConfiguring 以注册 AuditableEntityInterceptor + TenantQueryFilterInterceptor
            base.OnConfiguring(optionsBuilder);
        }

        protected override ITenantContext? TenantContext => _tenantContext;
    }

    /// <summary>
    /// 测试用 DbContext，不覆盖 <see cref="BaseDbContext.TenantContext"/>（默认 null），验证默认行为不变。
    /// </summary>
    private sealed class TestTenantDbContextNoTenant : BaseDbContext
    {
        public DbSet<TestTenantEntity> TenantEntities => Set<TestTenantEntity>();

        public TestTenantDbContextNoTenant(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase("fallback-no-tenant");
            }
            optionsBuilder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            // 调用基类 OnConfiguring 以注册 AuditableEntityInterceptor + TenantQueryFilterInterceptor
            base.OnConfiguring(optionsBuilder);
        }
    }

    /// <summary>
    /// 测试用多租户实体，继承 <see cref="Entity"/> 并实现 <see cref="ITenantEntity"/>。
    /// </summary>
    private sealed class TestTenantEntity : Entity, ITenantEntity
    {
        public string Name { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
    }
}
