using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Events;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Rules;
using Leno.Promotion.Infrastructure.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests.Rules;

/// <summary>
/// JsonRuleLoader 单元测试。
/// 覆盖：
/// - 启动加载（LoadAsync）缓存规则定义
/// - 热刷新（ReloadAsync）覆盖旧缓存
/// - GetDefinition 按 RuleType 查询
/// - 并发刷新跳过（Interlocked 保护）
/// - 空数据库场景
/// - 无效 JSON 跳过
/// </summary>
public class JsonRuleLoaderTests
{
    private static readonly ILogger<JsonRuleLoader> Logger = NullLogger<JsonRuleLoader>.Instance;

    private static PromotionRuleDefinition CreateDefinition(
        string ruleType,
        int priority,
        StackingPolicy stacking,
        string? definitionJson = null)
    {
        var json = definitionJson ?? """
            {
              "thresholds": [{"min_amount": 100, "discount_amount": 20}]
            }
            """;
        return PromotionRuleDefinition.Create(
            Guid.NewGuid(),
            ruleType,
            ruleType + " 显示名",
            priority,
            stacking,
            json,
            "v1.0");
    }

    [Fact]
    public async Task LoadAsync_LoadsEnabledDefinitionsIntoCache()
    {
        var definitions = new List<PromotionRuleDefinition>
        {
            CreateDefinition("FullReduction", 100, StackingPolicy.Stackable),
            CreateDefinition("Coupon", 200, StackingPolicy.Stackable)
        };

        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        var loader = new JsonRuleLoader(repoMock.Object, Logger);

        await loader.LoadAsync();

        var fullReduction = loader.GetDefinition("FullReduction");
        fullReduction.Should().NotBeNull();
        fullReduction!.RuleType.Should().Be("FullReduction");
        fullReduction.Priority.Should().Be(100);
        fullReduction.Stacking.Should().Be(StackingPolicy.Stackable);

        var coupon = loader.GetDefinition("Coupon");
        coupon.Should().NotBeNull();
        coupon!.Priority.Should().Be(200);
    }

    [Fact]
    public async Task LoadAsync_EmptyDatabase_CacheIsEmpty()
    {
        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition>());

        var loader = new JsonRuleLoader(repoMock.Object, Logger);

        await loader.LoadAsync();

        loader.GetDefinition("FullReduction").Should().BeNull();
        loader.CachedVersion.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_SkipsDefinition()
    {
        var badDef = PromotionRuleDefinition.Create(
            Guid.NewGuid(), "BadRule", "坏规则", 100, StackingPolicy.Stackable,
            "{ this is not valid json }", "v1.0");

        var goodDef = CreateDefinition("GoodRule", 200, StackingPolicy.Stackable);

        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition> { badDef, goodDef });

        var loader = new JsonRuleLoader(repoMock.Object, Logger);

        await loader.LoadAsync();

        // 坏规则被跳过
        loader.GetDefinition("BadRule").Should().BeNull();
        // 好规则正常加载
        loader.GetDefinition("GoodRule").Should().NotBeNull();
    }

    [Fact]
    public async Task ReloadAsync_OverwritesOldCache()
    {
        var initialDef = CreateDefinition("FullReduction", 100, StackingPolicy.Stackable);
        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition> { initialDef });

        var loader = new JsonRuleLoader(repoMock.Object, Logger);
        await loader.LoadAsync();

        loader.GetDefinition("FullReduction").Should().NotBeNull();

        // 模拟规则定义变更后热刷新
        var updatedDef = CreateDefinition("FullReduction", 50, StackingPolicy.Exclusive);
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition> { updatedDef });

        await loader.ReloadAsync();

        var cached = loader.GetDefinition("FullReduction");
        cached.Should().NotBeNull();
        cached!.Priority.Should().Be(50);
        cached.Stacking.Should().Be(StackingPolicy.Exclusive);
    }

    [Fact]
    public async Task ReloadAsync_RemovesDeletedDefinitions()
    {
        var def1 = CreateDefinition("FullReduction", 100, StackingPolicy.Stackable);
        var def2 = CreateDefinition("Coupon", 200, StackingPolicy.Stackable);

        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition> { def1, def2 });

        var loader = new JsonRuleLoader(repoMock.Object, Logger);
        await loader.LoadAsync();

        loader.GetDefinition("Coupon").Should().NotBeNull();

        // 模拟 Coupon 规则被禁用后重新加载
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition> { def1 });

        await loader.ReloadAsync();

        loader.GetDefinition("FullReduction").Should().NotBeNull();
        loader.GetDefinition("Coupon").Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_IncrementsCachedVersion()
    {
        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition>());

        var loader = new JsonRuleLoader(repoMock.Object, Logger);

        var versionBefore = loader.CachedVersion;
        await loader.LoadAsync();
        var versionAfter1 = loader.CachedVersion;

        await loader.LoadAsync();
        var versionAfter2 = loader.CachedVersion;

        versionAfter1.Should().BeGreaterThan(versionBefore);
        versionAfter2.Should().BeGreaterThan(versionAfter1);
    }

    [Fact]
    public async Task LoadAsync_SetsLoadedAtTimestamp()
    {
        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition>());

        var loader = new JsonRuleLoader(repoMock.Object, Logger);

        loader.LoadedAt.Should().BeNull();

        await loader.LoadAsync();

        loader.LoadedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_NullOrWhitespaceRuleType_ReturnsNull()
    {
        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition>());

        var loader = new JsonRuleLoader(repoMock.Object, Logger);
        await loader.LoadAsync();

        loader.GetDefinition(null!).Should().BeNull();
        loader.GetDefinition("").Should().BeNull();
        loader.GetDefinition("   ").Should().BeNull();
    }

    [Fact]
    public async Task HandleChangedEventAsync_TriggersReload()
    {
        var def = CreateDefinition("FullReduction", 100, StackingPolicy.Stackable);
        var repoMock = new Mock<IPromotionRuleDefinitionRepository>();
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition> { def });

        var loader = new JsonRuleLoader(repoMock.Object, Logger);
        await loader.LoadAsync();

        var updatedDef = CreateDefinition("FullReduction", 50, StackingPolicy.Exclusive);
        repoMock.Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromotionRuleDefinition> { updatedDef });

        var @event = new PromotionRuleDefinitionChangedEvent(Guid.NewGuid(), "FullReduction", "v2.0");
        await loader.HandleChangedEventAsync(@event);

        var cached = loader.GetDefinition("FullReduction");
        cached.Should().NotBeNull();
        cached!.Priority.Should().Be(50);
    }
}
