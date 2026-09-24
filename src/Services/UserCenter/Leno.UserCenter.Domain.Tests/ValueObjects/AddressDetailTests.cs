using Leno.UserCenter.Domain.ValueObjects;

namespace Leno.UserCenter.Domain.Tests.ValueObjects;

/// <summary>
/// AddressDetail（详细地址值对象）单元测试：长度边界、合法字符集、Trim 归一化。
/// </summary>
public class AddressDetailTests
{
    [Fact]
    public void Create_With_Valid_Detail_Should_Trim_And_Preserve()
    {
        var detail = AddressDetail.Create("  科技园路 1 号 A 栋 1001 室  ");

        detail.Value.Should().Be("科技园路 1 号 A 栋 1001 室");
        detail.ToString().Should().Be(detail.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_With_Blank_Should_Throw(string detail)
    {
        var create = () => AddressDetail.Create(detail);

        create.Should().Throw<ArgumentException>().WithMessage("*不可为空*");
    }

    [Fact]
    public void Create_With_Less_Than_5_Chars_Should_Throw()
    {
        var create = () => AddressDetail.Create("1234");

        create.Should().Throw<ArgumentException>().WithMessage("*5-200*");
    }

    [Fact]
    public void Create_With_5_Chars_Should_Be_At_Minimum_Boundary()
    {
        var create = () => AddressDetail.Create("12345");

        create.Should().NotThrow("5 字符为合法下边界");
    }

    [Fact]
    public void Create_With_Over_200_Chars_Should_Throw()
    {
        var create = () => AddressDetail.Create(new string('a', 201));

        create.Should().Throw<ArgumentException>().WithMessage("*5-200*");
    }

    [Fact]
    public void Create_With_200_Chars_Should_Be_At_Maximum_Boundary()
    {
        var create = () => AddressDetail.Create(new string('a', 200));

        create.Should().NotThrow("200 字符为合法上边界");
    }

    [Theory]
    [InlineData("科技园路<script>alert(1)</script>", "HTML 注入字符")]
    [InlineData("地址'单引号", "单引号")]
    [InlineData("地址\"双引号", "双引号")]
    public void Create_With_Illegal_Chars_Should_Throw(string detail, string reason)
    {
        var create = () => AddressDetail.Create(detail);

        create.Should().Throw<ArgumentException>().WithMessage("*非法字符*", reason);
    }

    [Theory]
    [InlineData("中山路 1 号（老楼）")]
    [InlineData("XX 巷 3 号-5 楼, B 座 #201")]
    [InlineData("XX 街道办 3 号楼 101 室, 单元 2")]
    public void Create_With_Legal_Special_Chars_Should_Pass(string detail)
    {
        var create = () => AddressDetail.Create(detail);

        create.Should().NotThrow();
    }
}
