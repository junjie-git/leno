using Leno.Testing.Fixtures;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Integration;

/// <summary>
/// 本测试程序集内的容器测试 collection 定义。
/// 根因：xUnit v2 只在测试程序集内查找 [CollectionDefinition]（含 ICollectionFixture 实现），
/// 基类 CrossBcIntegrationTestBase/DatabaseMigrationTestBase 上的
/// [Collection(ContainerCollection.Name)] 引用的是 Leno.Testing 程序集中的定义，
/// 跨程序集解析不到，导致注入 ContainerFixture 时报
/// "The following constructor parameters did not have matching fixture data: ContainerFixture fixture"。
/// 此处在测试程序集内以同名 collection 提供定义，使基类 attribute 正确解析。
/// </summary>
[CollectionDefinition(ContainerCollection.Name)]
public sealed class OrderTestContainerCollection : ICollectionFixture<ContainerFixture>
{
}
