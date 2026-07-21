using Leno.PointsMembership.Domain.Aggregates;

namespace Leno.PointsMembership.Domain.Tests;

/// <summary>
/// 验证 <see cref="MemberLevel.EvaluateLevel"/> 单遍扫描算法的正确性。
/// 关联审计 PM-L03：原先 OrderBy(MinGrowthValue) 后取最后匹配项，依赖 MinGrowthValue 与 Level 正相关假设；
/// 改为单遍按 Level 取最大匹配项，消除排序开销并正确处理 Level 与 MinGrowthValue 非单调的场景。
/// </summary>
public sealed class MemberLevelEvaluateLevelSinglePassTests
{
    [Fact]
    public void EvaluateLevel_Should_Return_Highest_Level_When_Levels_Unsorted()
    {
        // Arrange：等级定义乱序排列（非按 MinGrowthValue 升序）
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 4, "V4", 10000, 0, ""),
            MemberLevel.Create(Guid.NewGuid(), 2, "V2", 500, 2000, ""),
            MemberLevel.Create(Guid.NewGuid(), 0, "V0", 0, 100, ""),
            MemberLevel.Create(Guid.NewGuid(), 3, "V3", 2000, 10000, ""),
            MemberLevel.Create(Guid.NewGuid(), 1, "V1", 100, 500, "")
        };

        // Act
        var level = MemberLevel.EvaluateLevel(3000, levels);

        // Assert：应返回 V3（3000 >= 2000 且 < 10000），即使输入乱序
        level.Should().Be(3);
    }

    [Fact]
    public void EvaluateLevel_Should_Return_Highest_Level_By_LevelNumber_Not_MinGrowthValue()
    {
        // Arrange：构造 MinGrowthValue 与 Level 编号反向的场景
        // Level 3 的 MinGrowthValue=100，Level 1 的 MinGrowthValue=500
        // growthValue=600 同时满足两者，应返回 Level 3（编号更高），而非 Level 1（MinGrowthValue 更高）
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 3, "HighLevelLowThreshold", 100, 0, ""),
            MemberLevel.Create(Guid.NewGuid(), 1, "LowLevelHighThreshold", 500, 0, "")
        };

        // Act
        var level = MemberLevel.EvaluateLevel(600, levels);

        // Assert：按 Level 编号取最大匹配项，应为 3
        level.Should().Be(3);
    }

    [Fact]
    public void EvaluateLevel_Should_Return_Zero_When_No_Level_Qualified()
    {
        // Arrange：所有等级门槛均高于成长值
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 1, "V1", 100, 500, ""),
            MemberLevel.Create(Guid.NewGuid(), 2, "V2", 500, 2000, "")
        };

        // Act
        var level = MemberLevel.EvaluateLevel(50, levels);

        // Assert：无任何等级达标，返回 0
        level.Should().Be(0);
    }

    [Fact]
    public void EvaluateLevel_Should_Return_Zero_When_Levels_Empty()
    {
        // Act
        var level = MemberLevel.EvaluateLevel(1000, new List<MemberLevel>());

        // Assert：空集合返回 0
        level.Should().Be(0);
    }

    [Fact]
    public void EvaluateLevel_Should_Not_Perform_Orderby_On_MinGrowthValue()
    {
        // Arrange：验证单遍算法不依赖排序——若两个等级 MinGrowthValue 相同，
        // 应返回 Level 编号更高的那个（而非"最后遍历到的"）
        var levels = new List<MemberLevel>
        {
            MemberLevel.Create(Guid.NewGuid(), 1, "V1", 100, 0, ""),
            MemberLevel.Create(Guid.NewGuid(), 2, "V2", 100, 0, "")
        };

        // Act
        var level = MemberLevel.EvaluateLevel(150, levels);

        // Assert：两个等级门槛均为 100，growthValue=150 同时满足，应返回 Level 2
        level.Should().Be(2);
    }
}
