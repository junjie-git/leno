using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;
using Xunit;

namespace Leno.PointsMembership.Domain.Tests;

/// <summary>
/// 验证 PM-M02 修复：<see cref="Member.AddGrowthValue"/> 将 reason 参数写入
/// <see cref="MemberLevelChangeHistory"/> 子实体集合，不再忽略原因描述。
/// </summary>
public sealed class MemberAddGrowthValueReasonTests
{
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void AddGrowthValue_Should_Write_Reason_To_LevelChangeHistories()
    {
        var member = Member.Create(MemberId, UserId);

        member.AddGrowthValue(50, "每日签到（连续 7 天）");

        var history = Assert.Single(member.LevelChangeHistories);
        Assert.Equal("每日签到（连续 7 天）", history.Reason);
        Assert.Equal(50, history.GrowthValue);
    }

    [Fact]
    public void AddGrowthValue_Should_Record_Current_GrowthLevel_As_Old_And_New_Level()
    {
        var member = Member.Create(MemberId, UserId);

        member.AddGrowthValue(50, "签到");

        var history = Assert.Single(member.LevelChangeHistories);
        Assert.Equal(member.CurrentGrowthLevel, history.OldLevel);
        Assert.Equal(member.CurrentGrowthLevel, history.NewLevel);
    }

    [Fact]
    public void AddGrowthValue_Multiple_Times_Should_Append_Each_Reason_To_History()
    {
        var member = Member.Create(MemberId, UserId);

        member.AddGrowthValue(10, "签到返积分");
        member.AddGrowthValue(80, "订单消费返积分");
        member.AddGrowthValue(10, "评价返积分");

        Assert.Equal(3, member.LevelChangeHistories.Count);
        Assert.Equal("签到返积分", member.LevelChangeHistories[0].Reason);
        Assert.Equal("订单消费返积分", member.LevelChangeHistories[1].Reason);
        Assert.Equal("评价返积分", member.LevelChangeHistories[2].Reason);
        Assert.Equal(100, member.LevelChangeHistories[2].GrowthValue);
    }

    [Fact]
    public void AddGrowthValue_Should_Record_GrowthValue_After_Increment()
    {
        var member = Member.Create(MemberId, UserId);

        member.AddGrowthValue(30, "首次累加");

        var history = member.LevelChangeHistories[0];
        Assert.Equal(30, history.GrowthValue);
        Assert.Equal(30, member.GrowthValue);
    }
}
