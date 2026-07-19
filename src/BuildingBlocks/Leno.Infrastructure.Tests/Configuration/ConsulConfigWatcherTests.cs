using Consul;
using Leno.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Infrastructure.Tests.Configuration;

public class ConsulConfigWatcherTests
{
    [Fact]
    public async Task ExecuteAsync_ConfigChange_UpdatesConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("Service:Name", "order"),
            new KeyValuePair<string, string?>("AntiCorruption:UseGrpc", "false")
        }).Build();

        var consulMock = new Mock<IConsulClient>();
        var kvMock = new Mock<IKVEndpoint>();
        var callIndex = 0;
        var values = new[] { "false", "true" };

        kvMock.Setup(k => k.Get(It.IsAny<string>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string key, QueryOptions opts, CancellationToken ct) =>
              {
                  var idx = Interlocked.Increment(ref callIndex) - 1;
                  return new QueryResult<KVPair>
                  {
                      LastIndex = (ulong)(idx + 1),
                      Response = new KVPair(key) { Value = System.Text.Encoding.UTF8.GetBytes(values[Math.Min(idx, values.Length - 1)]) }
                  };
              });
        consulMock.SetupGet(c => c.KV).Returns(kvMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var watcher = new ConsulConfigWatcher(
            consulMock.Object, config,
            NullLogger<ConsulConfigWatcher>.Instance);

        // Act
        await watcher.StartAsync(cts.Token);
        await Task.Delay(1500);  // 等待 watcher 处理
        await watcher.StopAsync(cts.Token);

        // Assert
        config["AntiCorruption:UseGrpc"].Should().Be("true");
    }

    [Fact]
    public async Task ExecuteAsync_ConsulError_RetriesWithoutCrash()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("Service:Name", "order"),
            new KeyValuePair<string, string?>("AntiCorruption:UseGrpc", "false")
        }).Build();

        var consulMock = new Mock<IConsulClient>();
        var kvMock = new Mock<IKVEndpoint>();
        kvMock.Setup(k => k.Get(It.IsAny<string>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new Exception("consul down"));
        consulMock.SetupGet(c => c.KV).Returns(kvMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var watcher = new ConsulConfigWatcher(
            consulMock.Object, config,
            NullLogger<ConsulConfigWatcher>.Instance);

        // Act
        await watcher.StartAsync(cts.Token);
        await Task.Delay(800);
        await watcher.StopAsync(cts.Token);

        // Assert
        config["AntiCorruption:UseGrpc"].Should().Be("false");  // 保持原值
    }
}
