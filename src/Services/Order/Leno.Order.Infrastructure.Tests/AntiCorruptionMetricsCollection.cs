using Xunit;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// 防腐层指标测试专用 collection：禁用并行。
/// 根因（run #10 build-solution 4 个指标测试失败）：AntiCorruptionMetrics 使用进程级
/// 静态 Meter/Counter，AntiCorruptionServicesTests、OrderTimeoutDelayMessageConsumerTests
/// 等其他类在 xUnit 默认跨类并行下并发调用同一防腐层服务，其失败计数被投递到
/// AntiCorruptionMetricsTests 正在监听的 MeterListener，造成 captured 列表串扰
/// （CI 日志实证：Points 用例捕获到 promotion/lock_coupon 条目、LockCoupon 用例捕获到
/// points/freeze 条目）。DisableParallelization 让本 collection 排他运行。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AntiCorruptionMetricsCollection
{
    public const string Name = "AntiCorruptionMetrics";
}
