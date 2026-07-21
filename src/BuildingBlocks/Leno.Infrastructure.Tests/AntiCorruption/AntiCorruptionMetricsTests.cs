using Leno.Infrastructure.AntiCorruption;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class AntiCorruptionMetricsTests
{
    [Fact]
    public void UpdateCircuitOpenState_ConcurrentWrites_ShouldNotThrow()
    {
        // Arrange
        AntiCorruptionMetrics.Initialize();
        var services = new[] { "svc-a", "svc-b", "svc-c", "svc-d", "svc-e" };
        var exceptions = new ConcurrentBag<Exception>();

        // Act — 50 线程并发写入不同 service
        Parallel.For(0, 50, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            try
            {
                var svc = services[i % services.Length];
                AntiCorruptionMetrics.UpdateCircuitOpenState(svc, i % 2 == 0);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        exceptions.Should().BeEmpty("并发写入 _circuitOpenStates 不应抛出异常");
    }

    [Fact]
    public void UpdateCircuitOpenState_ConcurrentWriteAndEnumerate_ShouldNotThrow()
    {
        // Arrange
        AntiCorruptionMetrics.Initialize();
        var exceptions = new ConcurrentBag<Exception>();

        var field = typeof(AntiCorruptionMetrics).GetField("_circuitOpenStates",
            BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("_circuitOpenStates 字段应存在");

        // Act — 并发写入 + 同时枚举 _circuitOpenStates（模拟 OTLP ObservableGauge 回调枚举）
        var writeTask = Task.Run(() =>
        {
            Parallel.For(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
            {
                try
                {
                    AntiCorruptionMetrics.UpdateCircuitOpenState($"svc-{i % 10}", i % 2 == 0);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        });

        var enumerateTask = Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    // 通过反射枚举 _circuitOpenStates，模拟 ObservableGauge 的 observeValues 回调
                    var dict = (IDictionary)field!.GetValue(null)!;
                    foreach (var kv in dict)
                    {
                        _ = kv;
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        Task.WaitAll(writeTask, enumerateTask);

        // Assert
        exceptions.Should().BeEmpty("并发写入与枚举不应抛出 Collection was modified 异常");
    }
}
