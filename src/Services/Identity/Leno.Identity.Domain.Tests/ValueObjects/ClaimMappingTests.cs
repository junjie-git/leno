using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Domain.Tests.ValueObjects;

/// <summary>
/// ClaimMapping 值对象单元测试（Identity BC，3.7 OAuth/SSO 通用化）。
/// 验证 record 相等性与解构语义。
/// </summary>
public class ClaimMappingTests
{
    [Fact]
    public void Records_With_Same_Values_Should_Be_Equal()
    {
        var first = new ClaimMapping("email", "mail");
        var second = new ClaimMapping("email", "mail");

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Records_With_Different_SourceClaim_Should_Not_Be_Equal()
    {
        var first = new ClaimMapping("email", "mail");
        var second = new ClaimMapping("name", "mail");

        first.Should().NotBe(second);
        (first != second).Should().BeTrue();
    }

    [Fact]
    public void Records_With_Different_TargetClaim_Should_Not_Be_Equal()
    {
        var first = new ClaimMapping("email", "mail");
        var second = new ClaimMapping("email", "email");

        first.Should().NotBe(second);
    }

    [Fact]
    public void Deconstruct_Should_Return_Source_And_Target()
    {
        var mapping = new ClaimMapping("picture", "avatar_url");

        var (source, target) = mapping;

        source.Should().Be("picture");
        target.Should().Be("avatar_url");
    }

    [Fact]
    public void SourceClaim_And_TargetClaim_Should_Preserve_Values()
    {
        var mapping = new ClaimMapping("sub", "sub");

        mapping.SourceClaim.Should().Be("sub");
        mapping.TargetClaim.Should().Be("sub");
    }
}
