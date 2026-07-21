using Leno.Order.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Net;

namespace Leno.Order.Infrastructure.Tests;

/// <summary>
/// StockReconciliationService 单元测试，验证使用 SCAN（KeysAsync）异步分页扫描替代 KEYS（Keys）同步阻塞扫描。
/// </summary>
public class StockReconciliationServiceTests
{
    [Fact]
    public async Task ReconcileAsync_Should_Use_Scan_Not_Keys()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var serverMock = new Mock<IServer>();
        var dbMock = new Mock<IDatabase>();

        var endpoint = new DnsEndPoint("localhost", 6379);
        redisMock.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { endpoint });
        redisMock.Setup(r => r.GetServer(endpoint, It.IsAny<object>())).Returns(serverMock.Object);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        // 使用合法 GUID 构造库存键，使生产代码能解析 skuId 并继续执行
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        var scanResults = new List<RedisKey>
        {
            (RedisKey)$"inventory:stock:{skuId1}",
            (RedisKey)$"inventory:stock:{skuId2}"
        };

        // 模拟 SCAN（KeysAsync）返回分页结果；预占键扫描返回空列表
        var emptyResults = new List<RedisKey>();
        var callCount = 0;
        serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(() =>
            {
                callCount++;
                // 第一次调用扫描库存键，后续调用扫描预占键（返回空）
                var results = callCount == 1 ? scanResults : emptyResults;
                return GetAsyncEnumerable(results);
            });

        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(100);

        var loggerMock = new Mock<ILogger<StockReconciliationService>>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var sut = new StockReconciliationService(scopeFactoryMock.Object, redisMock.Object, loggerMock.Object);

        // Act：通过反射调用内部 ReconcileAsync
        await InvokeReconcileAsync(sut, CancellationToken.None);

        // Assert：应调用 KeysAsync（SCAN）而非 Keys（KEYS）
        serverMock.Verify(
            s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()),
            Times.AtLeastOnce);
        serverMock.Verify(
            s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    /// <summary>
    /// 构造 IAsyncEnumerable&lt;RedisKey&gt; 用于模拟 SCAN 异步分页返回结果。
    /// </summary>
    private static async IAsyncEnumerable<RedisKey> GetAsyncEnumerable(IEnumerable<RedisKey> keys)
    {
        foreach (var key in keys)
        {
            await Task.Yield();
            yield return key;
        }
    }

    /// <summary>
    /// 通过反射调用 StockReconciliationService 的私有 ReconcileAsync 方法。
    /// </summary>
    private static async Task InvokeReconcileAsync(StockReconciliationService service, CancellationToken ct)
    {
        var method = typeof(StockReconciliationService).GetMethod(
            "ReconcileAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { ct })!;
    }
}
