using Leno.Order.Domain.ValueObjects;

namespace Leno.Order.Domain.Tests;

public class LogisticsTraceResultTests
{
    [Fact]
    public void Constructor_ValidInput_ShouldSetProperties()
    {
        var nodes = new List<LogisticsTraceNode>
        {
            new("已揽收", new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), "深圳"),
            new("运输中", new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), "广州")
        };

        var result = new LogisticsTraceResult("SF123", "SF", nodes, false);

        result.LogisticsNo.Should().Be("SF123");
        result.CompanyCode.Should().Be("SF");
        result.Nodes.Should().HaveCount(2);
        result.IsFromCache.Should().BeFalse();
    }

    [Fact]
    public void Constructor_EmptyInput_ShouldSetEmptyDefaults()
    {
        var result = new LogisticsTraceResult("", "", Array.Empty<LogisticsTraceNode>(), false);

        result.LogisticsNo.Should().BeEmpty();
        result.CompanyCode.Should().BeEmpty();
        result.Nodes.Should().BeEmpty();
        result.IsFromCache.Should().BeFalse();
    }

    [Fact]
    public void Constructor_FromCache_ShouldSetIsFromCache()
    {
        var result = new LogisticsTraceResult("SF123", "SF", Array.Empty<LogisticsTraceNode>(), true);

        result.IsFromCache.Should().BeTrue();
    }

    [Fact]
    public void Empty_ShouldCreateEmptyResult()
    {
        var result = LogisticsTraceResult.Empty("SF123", "SF");

        result.LogisticsNo.Should().Be("SF123");
        result.CompanyCode.Should().Be("SF");
        result.Nodes.Should().BeEmpty();
        result.IsFromCache.Should().BeFalse();
    }

    [Fact]
    public void EmptyFromCache_ShouldCreateEmptyResultWithCacheFlag()
    {
        var result = LogisticsTraceResult.EmptyFromCache("SF123", "SF");

        result.LogisticsNo.Should().Be("SF123");
        result.CompanyCode.Should().Be("SF");
        result.Nodes.Should().BeEmpty();
        result.IsFromCache.Should().BeTrue();
    }

    [Fact]
    public void Nodes_ShouldBeReadOnly()
    {
        var result = new LogisticsTraceResult("SF123", "SF", new List<LogisticsTraceNode>(), false);

        result.Nodes.Should().BeAssignableTo<IReadOnlyList<LogisticsTraceNode>>();
    }
}

public class LogisticsTraceNodeTests
{
    [Fact]
    public void Constructor_ValidInput_ShouldSetProperties()
    {
        var occurredAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var node = new LogisticsTraceNode("已揽收", occurredAt, "深圳");

        node.Description.Should().Be("已揽收");
        node.OccurredAt.Should().Be(occurredAt);
        node.Location.Should().Be("深圳");
    }

    [Fact]
    public void Constructor_EmptyStrings_ShouldSetEmptyStrings()
    {
        var node = new LogisticsTraceNode("", DateTime.UtcNow, "");

        node.Description.Should().BeEmpty();
        node.Location.Should().BeEmpty();
    }
}