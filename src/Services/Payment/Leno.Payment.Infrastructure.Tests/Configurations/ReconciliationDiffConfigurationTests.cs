using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

namespace Leno.Payment.Infrastructure.Tests.Configurations;

/// <summary>
/// P1-14 测试：验证 ReconciliationDiffConfiguration 表名与枚举 HasConversion 与 Payment BC 一致。
/// 根因：原配置使用 PascalCase 表名 "ReconciliationDiffs"，与 payment_orders / refund_orders 等
/// snake_case 规范不一致；Channel/DiffType/Status 枚举使用 HasConversion&lt;string&gt;()，
/// 而 PaymentOrderConfiguration / RefundOrderConfiguration 同枚举使用 HasConversion&lt;int&gt;()，
/// 同一枚举在不同表中存储类型不一致，导致跨表对账查询与索引效率不齐。
/// 修复后：表名 snake_case；三个枚举统一 HasConversion&lt;int&gt;()。
/// </summary>
/// <remarks>
/// 由于测试环境未引入 EF Core Sqlite/InMemory provider，无法直接验证数据库层。
/// 本测试通过 ModelBuilder 直接调用 Configuration.Configure，再读取 IMutableModel 元数据
/// 断言表名与枚举转换类型。
/// </remarks>
public class ReconciliationDiffConfigurationTests
{
    private static IMutableEntityType BuildEntityType()
    {
        var modelBuilder = new ModelBuilder();
        var entityTypeBuilder = modelBuilder.Entity<ReconciliationDiff>();
        var configuration = new ReconciliationDiffConfiguration();
        configuration.Configure(entityTypeBuilder);
        return modelBuilder.Model.FindEntityType(typeof(ReconciliationDiff))!;
    }

    [Fact]
    public void Configure_ShouldUseSnakeCaseTableName()
    {
        // 安排 / 行动
        var entityType = BuildEntityType();

        // 断言：表名为 snake_case "reconciliation_diffs"，不再是 PascalCase "ReconciliationDiffs"
        var tableName = entityType.GetTableName();
        Assert.Equal("reconciliation_diffs", tableName);
    }

    [Fact]
    public void Configure_Channel_ShouldUseIntConversion()
    {
        // 安排 / 行动
        var entityType = BuildEntityType();

        // 断言：Channel 枚举使用 int 转换（与 PaymentOrderConfiguration / RefundOrderConfiguration 一致）。
        // EF Core 6+ 的 HasConversion<int>() 短形式仅记录 ProviderClrType 注解（int），
        // 显式 ValueConverter 实例要到类型映射动画阶段才生成，
        // 因此 GetValueConverter() 返回 null——按官方可观察效果断言 ProviderClrType == int。
        var channelProperty = entityType.FindProperty(nameof(ReconciliationDiff.Channel));
        Assert.NotNull(channelProperty);
        Assert.Equal(typeof(PaymentChannel), channelProperty!.ClrType);
        Assert.Equal(typeof(int), GetEffectiveProviderClrType(channelProperty));
    }

    [Fact]
    public void Configure_DiffType_ShouldUseIntConversion()
    {
        // 安排 / 行动
        var entityType = BuildEntityType();

        // 断言：DiffType 枚举使用 int 转换
        var diffTypeProperty = entityType.FindProperty(nameof(ReconciliationDiff.DiffType));
        Assert.NotNull(diffTypeProperty);
        Assert.Equal(typeof(ReconciliationDiffType), diffTypeProperty!.ClrType);
        Assert.Equal(typeof(int), GetEffectiveProviderClrType(diffTypeProperty));
    }

    [Fact]
    public void Configure_Status_ShouldUseIntConversion()
    {
        // 安排 / 行动
        var entityType = BuildEntityType();

        // 断言：Status 枚举使用 int 转换
        var statusProperty = entityType.FindProperty(nameof(ReconciliationDiff.Status));
        Assert.NotNull(statusProperty);
        Assert.Equal(typeof(ReconciliationDiffStatus), statusProperty!.ClrType);
        Assert.Equal(typeof(int), GetEffectiveProviderClrType(statusProperty));
    }

    /// <summary>
    /// 获取属性的生效数据库提供方类型：
    /// 优先读取显式 ValueConverter 的 ProviderClrType，回退到 HasConversion&lt;T&gt;() 短形式
    /// 记录的 ProviderClrType 注解（EF Core 6+ 短形式不产生显式 converter 实例）。
    /// </summary>
    private static Type? GetEffectiveProviderClrType(IMutableProperty property)
    {
        return property.GetValueConverter()?.ProviderClrType ?? property.GetProviderClrType();
    }
}
