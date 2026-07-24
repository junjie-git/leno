using Leno.ApiGateway.Bff.Dag;

namespace Leno.ApiGateway.Bff.Dag.Tests;

/// <summary>
/// 拓扑排序器单元测试：覆盖 Kahn 算法的线性图、菱形依赖、环检测、缺失依赖等场景。
/// </summary>
public class TopologicalSorterTests
{
    private static readonly string[] ThreeIndependentNodeNames = { "A", "B", "C" };

    private static AggregateNode CreateNode(string name, params string[] deps)
    {
        var node = new AggregateNode(name, (_, _) => Task.FromResult<object?>(name), TimeSpan.FromSeconds(1));
        foreach (var d in deps)
        {
            node.Dependencies.Add(d);
        }
        return node;
    }

    [Fact]
    public void Sort_EmptyCollection_ReturnsEmptyList()
    {
        var sorted = TopologicalSorter.Sort(Array.Empty<AggregateNode>());

        sorted.Should().BeEmpty();
    }

    [Fact]
    public void Sort_SingleNode_ReturnsSingleNode()
    {
        var node = CreateNode("A");

        var sorted = TopologicalSorter.Sort(new[] { node });

        sorted.Should().HaveCount(1);
        sorted[0].Name.Should().Be("A");
    }

    [Fact]
    public void Sort_ThreeIndependentNodes_ReturnsAllThree()
    {
        var nodes = new[] { CreateNode("A"), CreateNode("B"), CreateNode("C") };

        var sorted = TopologicalSorter.Sort(nodes);

        sorted.Should().HaveCount(3);
        sorted.Select(n => n.Name).Should().BeEquivalentTo(ThreeIndependentNodeNames);
    }

    [Fact]
    public void Sort_LinearChain_ReturnsInDependencyOrder()
    {
        // C → B → A（C 依赖 B，B 依赖 A，排序后 A 在前）
        var nodes = new[]
        {
            CreateNode("C", "B"),
            CreateNode("B", "A"),
            CreateNode("A")
        };

        var sorted = TopologicalSorter.Sort(nodes);

        sorted.Should().HaveCount(3);
        sorted[0].Name.Should().Be("A");
        sorted[1].Name.Should().Be("B");
        sorted[2].Name.Should().Be("C");
    }

    [Fact]
    public void Sort_DiamondDependency_ReturnsValidOrder()
    {
        // D → {B, C} → A：A 最先，B 和 C 中间（顺序不限），D 最后
        var nodes = new[]
        {
            CreateNode("D", "B", "C"),
            CreateNode("C", "A"),
            CreateNode("B", "A"),
            CreateNode("A")
        };

        var sorted = TopologicalSorter.Sort(nodes);

        sorted.Should().HaveCount(4);
        sorted[0].Name.Should().Be("A");
        sorted[3].Name.Should().Be("D");
        // B 和 C 都在 A 之后、D 之前
        var bIndex = Array.FindIndex(sorted.ToArray(), n => n.Name == "B");
        var cIndex = Array.FindIndex(sorted.ToArray(), n => n.Name == "C");
        bIndex.Should().BeGreaterThan(0);
        cIndex.Should().BeGreaterThan(0);
        bIndex.Should().BeLessThan(3);
        cIndex.Should().BeLessThan(3);
    }

    [Fact]
    public void Sort_TwoNodeCycle_ThrowsInvalidOperationException()
    {
        // A → B → A
        var nodes = new[]
        {
            CreateNode("A", "B"),
            CreateNode("B", "A")
        };

        var act = () => TopologicalSorter.Sort(nodes);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*环依赖*");
    }

    [Fact]
    public void Sort_ThreeNodeCycle_ThrowsInvalidOperationException()
    {
        // A → B → C → A
        var nodes = new[]
        {
            CreateNode("A", "C"),
            CreateNode("B", "A"),
            CreateNode("C", "B")
        };

        var act = () => TopologicalSorter.Sort(nodes);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*环依赖*");
    }

    [Fact]
    public void Sort_SelfLoop_ThrowsInvalidOperationException()
    {
        // A 依赖自身（绕过 AggregateBuilder 的自依赖检查直接构造节点）
        var nodes = new[] { CreateNode("A", "A") };

        var act = () => TopologicalSorter.Sort(nodes);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*环依赖*");
    }

    [Fact]
    public void Sort_MissingDependency_ThrowsInvalidOperationException()
    {
        // A 依赖不存在的 X
        var nodes = new[] { CreateNode("A", "X") };

        var act = () => TopologicalSorter.Sort(nodes);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*不存在*");
    }

    [Fact]
    public void Sort_NullCollection_ThrowsArgumentNullException()
    {
        var act = () => TopologicalSorter.Sort(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
