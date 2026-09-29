using Leno.Testing.Fixtures;
using Xunit;

namespace Leno.Inventory.Infrastructure.Tests.Integration;

/// <summary>
/// 本测试程序集内的容器测试 collection 定义。
/// xUnit v2 只在测试程序集内查找 [CollectionDefinition]（含 ICollectionFixture 实现），
/// 基类 CrossBcIntegrationTestBase 上的 [Collection(ContainerCollection.Name)] 引用的是
/// Leno.Testing 程序集中的定义，跨程序集解析不到，导致 ContainerFixture 注入失败。
/// 此处在测试程序集内以同名 collection 提供定义（与 Order/Cart 测试程序集同一模式）。
/// </summary>
[CollectionDefinition(ContainerCollection.Name)]
public sealed class InventoryTestContainerCollection : ICollectionFixture<ContainerFixture>
{
}
