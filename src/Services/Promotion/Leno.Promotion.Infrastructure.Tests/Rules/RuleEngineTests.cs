using Leno.Promotion.Domain.Rules;
using Leno.Promotion.Infrastructure.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.Promotion.Infrastructure.Tests.Rules;

/// <summary>
/// RuleEngine 编排器单元测试。
/// 覆盖：
/// - 优先级排序（Priority 升序）
/// - Exclusive 中断后续评估
/// - Stackable 叠加扣减
/// - BestOf 组内取最优
/// - 混合策略编排
/// - 无规则适用
/// </summary>
public class RuleEngineTests
{
    private static readonly ILogger<RuleEngine> Logger = NullLogger<RuleEngine>.Instance;

    private static PromotionRuleContext CreateContext(decimal subTotal)
    {
        return new PromotionRuleContext
        {
            UserId = 1,
            SellerId = 1,
            Items = new List<CartItemContext>
            {
                new() { SkuId = Guid.NewGuid(), Quantity = 1, UnitPrice = subTotal }
            },
            SubTotal = subTotal,
            Attributes = new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// 测试桩规则：返回预定义的适用性与评估结果，便于验证编排逻辑。
    /// </summary>
    private sealed class StubRule : IPromotionRule
    {
        private readonly bool _applicable;
        private readonly decimal _discount;
        private readonly Guid? _couponId;

        public StubRule(
            string ruleType,
            StackingPolicy stacking,
            int priority,
            bool applicable,
            decimal discount,
            Guid? couponId = null)
        {
            RuleType = ruleType;
            Stacking = stacking;
            Priority = priority;
            _applicable = applicable;
            _discount = discount;
            _couponId = couponId;
        }

        public string RuleType { get; }
        public StackingPolicy Stacking { get; }
        public int Priority { get; }

        public Task<bool> IsApplicableAsync(PromotionRuleContext context, CancellationToken ct)
            => Task.FromResult(_applicable);

        public Task<PromotionRuleResult> EvaluateAsync(PromotionRuleContext context, CancellationToken ct)
        {
            if (!_applicable || _discount <= 0m)
            {
                return Task.FromResult(PromotionRuleResult.NotApplied(RuleType, "测试桩未应用"));
            }

            return Task.FromResult(PromotionRuleResult.AppliedResult(
                RuleType, _discount, _couponId,
                new Dictionary<string, string> { ["stub"] = RuleType }));
        }
    }

    [Fact]
    public async Task EvaluateAsync_NoRules_ReturnsZeroDiscount()
    {
        var engine = new RuleEngine(Enumerable.Empty<IPromotionRule>(), Logger);
        var context = CreateContext(100m);

        var result = await engine.EvaluateAsync(context, CancellationToken.None);

        result.TotalDiscountAmount.Should().Be(0m);
        result.AppliedRules.Should().BeEmpty();
        result.OriginalSubTotal.Should().Be(100m);
        result.PayableAmount.Should().Be(100m);
    }

    [Fact]
    public async Task EvaluateAsync_SortsByPriorityAscending()
    {
        // 注册顺序与优先级相反，验证引擎按 Priority 升序评估
        var rules = new List<IPromotionRule>
        {
            new StubRule("LateRule", StackingPolicy.Stackable, 200, true, 10m),
            new StubRule("EarlyRule", StackingPolicy.Stackable, 100, true, 20m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(2);
        // EarlyRule（Priority=100）应先于 LateRule（Priority=200）
        result.AppliedRules[0].RuleType.Should().Be("EarlyRule");
        result.AppliedRules[1].RuleType.Should().Be("LateRule");
        result.TotalDiscountAmount.Should().Be(30m);
    }

    [Fact]
    public async Task EvaluateAsync_Exclusive_BreaksAfterApply()
    {
        // RuleA（Exclusive, Priority=50）应用后应中断，RuleB（Priority=200）不应被评估
        var rules = new List<IPromotionRule>
        {
            new StubRule("RuleA", StackingPolicy.Exclusive, 50, true, 30m),
            new StubRule("RuleB", StackingPolicy.Stackable, 200, true, 50m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(1);
        result.AppliedRules[0].RuleType.Should().Be("RuleA");
        result.TotalDiscountAmount.Should().Be(30m);
    }

    [Fact]
    public async Task EvaluateAsync_Exclusive_NotApplied_ContinuesEvaluation()
    {
        // Exclusive 规则未应用时不应中断，后续规则应继续评估
        var rules = new List<IPromotionRule>
        {
            new StubRule("RuleA", StackingPolicy.Exclusive, 50, false, 0m),
            new StubRule("RuleB", StackingPolicy.Stackable, 100, true, 20m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(1);
        result.AppliedRules[0].RuleType.Should().Be("RuleB");
        result.TotalDiscountAmount.Should().Be(20m);
    }

    [Fact]
    public async Task EvaluateAsync_Stackable_AccumulatesDiscounts()
    {
        var rules = new List<IPromotionRule>
        {
            new StubRule("RuleA", StackingPolicy.Stackable, 100, true, 20m),
            new StubRule("RuleB", StackingPolicy.Stackable, 200, true, 30m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(2);
        result.TotalDiscountAmount.Should().Be(50m);
    }

    [Fact]
    public async Task EvaluateAsync_BestOf_KeepsOnlyMaxDiscount()
    {
        // 两个 BestOf 规则同组，仅保留折扣最大者
        var rules = new List<IPromotionRule>
        {
            new StubRule("RuleA", StackingPolicy.BestOf, 100, true, 15m),
            new StubRule("RuleB", StackingPolicy.BestOf, 200, true, 40m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(1);
        result.AppliedRules[0].RuleType.Should().Be("RuleB");
        result.TotalDiscountAmount.Should().Be(40m);
    }

    [Fact]
    public async Task EvaluateAsync_BestOf_SameDiscount_KeepsFirstByPriority()
    {
        // 同折扣取优先级更高者（首个，因已按 Priority 升序排列）
        var rules = new List<IPromotionRule>
        {
            new StubRule("HighPriority", StackingPolicy.BestOf, 100, true, 30m),
            new StubRule("LowPriority", StackingPolicy.BestOf, 200, true, 30m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(1);
        result.AppliedRules[0].RuleType.Should().Be("HighPriority");
        result.TotalDiscountAmount.Should().Be(30m);
    }

    [Fact]
    public async Task EvaluateAsync_BestOf_AllNotApplied_ReturnsEmpty()
    {
        var rules = new List<IPromotionRule>
        {
            new StubRule("RuleA", StackingPolicy.BestOf, 100, false, 0m),
            new StubRule("RuleB", StackingPolicy.BestOf, 200, false, 0m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().BeEmpty();
        result.TotalDiscountAmount.Should().Be(0m);
    }

    [Fact]
    public async Task EvaluateAsync_Mixed_StackableThenBestOf()
    {
        // Stackable 规则先扣减，随后 BestOf 组基于剩余 SubTotal 竞争
        var rules = new List<IPromotionRule>
        {
            new StubRule("StackableA", StackingPolicy.Stackable, 100, true, 20m),
            new StubRule("BestOfA", StackingPolicy.BestOf, 200, true, 10m),
            new StubRule("BestOfB", StackingPolicy.BestOf, 300, true, 25m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        // StackableA 扣减 20，BestOf 组取最大 25
        result.AppliedRules.Should().HaveCount(2);
        result.AppliedRules[0].RuleType.Should().Be("StackableA");
        result.AppliedRules[1].RuleType.Should().Be("BestOfB");
        result.TotalDiscountAmount.Should().Be(45m);
    }

    [Fact]
    public async Task EvaluateAsync_Mixed_BestOfThenStackable()
    {
        // BestOf 组先解析，取最大者，然后 Stackable 规则基于剩余 SubTotal 继续
        var rules = new List<IPromotionRule>
        {
            new StubRule("BestOfA", StackingPolicy.BestOf, 100, true, 15m),
            new StubRule("BestOfB", StackingPolicy.BestOf, 150, true, 35m),
            new StubRule("StackableA", StackingPolicy.Stackable, 200, true, 10m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        // BestOf 组取 35，StackableA 再扣 10
        result.AppliedRules.Should().HaveCount(2);
        result.AppliedRules[0].RuleType.Should().Be("BestOfB");
        result.AppliedRules[1].RuleType.Should().Be("StackableA");
        result.TotalDiscountAmount.Should().Be(45m);
    }

    [Fact]
    public async Task EvaluateAsync_Mixed_ExclusiveBeforeStackable()
    {
        // Exclusive 规则先应用并中断，Stackable 规则不评估
        var rules = new List<IPromotionRule>
        {
            new StubRule("ExclusiveA", StackingPolicy.Exclusive, 50, true, 50m),
            new StubRule("StackableA", StackingPolicy.Stackable, 100, true, 20m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(1);
        result.AppliedRules[0].RuleType.Should().Be("ExclusiveA");
        result.TotalDiscountAmount.Should().Be(50m);
    }

    [Fact]
    public async Task EvaluateAsync_CollectsAppliedCouponId()
    {
        var couponId = Guid.NewGuid();
        var rules = new List<IPromotionRule>
        {
            new StubRule("CouponRule", StackingPolicy.Stackable, 100, true, 20m, couponId),
            new StubRule("FullReduction", StackingPolicy.Stackable, 200, true, 10m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedCouponId.Should().Be(couponId);
    }

    [Fact]
    public async Task EvaluateAsync_NullContext_ThrowsArgumentNullException()
    {
        var engine = new RuleEngine(Enumerable.Empty<IPromotionRule>(), Logger);

        var act = async () => await engine.EvaluateAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateAsync_SkipsNotApplicableRules()
    {
        var rules = new List<IPromotionRule>
        {
            new StubRule("NotApplicable", StackingPolicy.Stackable, 100, false, 30m),
            new StubRule("Applicable", StackingPolicy.Stackable, 200, true, 20m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(1);
        result.AppliedRules[0].RuleType.Should().Be("Applicable");
        result.TotalDiscountAmount.Should().Be(20m);
    }

    [Fact]
    public async Task EvaluateAsync_BestOfGroupAtEnd_GetsResolved()
    {
        // BestOf 规则在列表末尾，评估结束时应解析
        var rules = new List<IPromotionRule>
        {
            new StubRule("StackableA", StackingPolicy.Stackable, 100, true, 10m),
            new StubRule("BestOfA", StackingPolicy.BestOf, 200, true, 20m),
            new StubRule("BestOfB", StackingPolicy.BestOf, 300, true, 35m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(100m), CancellationToken.None);

        result.AppliedRules.Should().HaveCount(2);
        result.AppliedRules[1].RuleType.Should().Be("BestOfB");
        result.TotalDiscountAmount.Should().Be(45m);
    }

    [Fact]
    public async Task EvaluateAsync_PayableAmount_NeverNegative()
    {
        // 折扣超过 SubTotal 时实付不应为负
        var rules = new List<IPromotionRule>
        {
            new StubRule("BigDiscount", StackingPolicy.Stackable, 100, true, 200m)
        };
        var engine = new RuleEngine(rules, Logger);

        var result = await engine.EvaluateAsync(CreateContext(50m), CancellationToken.None);

        result.TotalDiscountAmount.Should().Be(200m);
        result.PayableAmount.Should().Be(0m);
    }
}
