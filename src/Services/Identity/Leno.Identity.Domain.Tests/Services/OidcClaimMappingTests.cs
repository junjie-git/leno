using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Domain.Tests.Services;

/// <summary>
/// OidcClaimMapping 单元测试（Identity BC，3.7 OAuth/SSO 通用化）。
/// 覆盖默认映射、合并策略与自定义覆盖语义。
/// </summary>
public class OidcClaimMappingTests
{
    [Fact]
    public void Default_Should_Contain_Standard_Oidc_Claim_Mappings()
    {
        var mapping = OidcClaimMapping.Default;

        mapping.Mappings.Should().HaveCount(4);
        mapping.Mappings.Should().Contain(m => m.SourceClaim == "sub" && m.TargetClaim == "sub");
        mapping.Mappings.Should().Contain(m => m.SourceClaim == "email" && m.TargetClaim == "email");
        mapping.Mappings.Should().Contain(m => m.SourceClaim == "name" && m.TargetClaim == "name");
        mapping.Mappings.Should().Contain(m => m.SourceClaim == "preferred_username" && m.TargetClaim == "preferred_username");
    }

    [Fact]
    public void Merge_With_Custom_Overriding_Base_Should_Replace_Same_SourceClaim()
    {
        var @base = OidcClaimMapping.Default;
        var custom = new OidcClaimMapping
        {
            Mappings = new List<ClaimMapping>
            {
                new("email", "mail")
            }
        };

        var merged = OidcClaimMapping.Merge(@base, custom);

        // 自定义 email → mail 应存在，base 的 email → email 应被移除
        merged.Mappings.Should().Contain(m => m.SourceClaim == "email" && m.TargetClaim == "mail");
        merged.Mappings.Should().NotContain(m => m.SourceClaim == "email" && m.TargetClaim == "email");
        // 其余 base 规则保留
        merged.Mappings.Should().Contain(m => m.SourceClaim == "sub" && m.TargetClaim == "sub");
        merged.Mappings.Should().Contain(m => m.SourceClaim == "name" && m.TargetClaim == "name");
        merged.Mappings.Should().Contain(m => m.SourceClaim == "preferred_username" && m.TargetClaim == "preferred_username");
    }

    [Fact]
    public void Merge_With_Custom_New_SourceClaim_Should_Append_To_Base()
    {
        var @base = OidcClaimMapping.Default;
        var custom = new OidcClaimMapping
        {
            Mappings = new List<ClaimMapping>
            {
                new("picture", "avatar_url")
            }
        };

        var merged = OidcClaimMapping.Merge(@base, custom);

        merged.Mappings.Should().HaveCount(5);
        merged.Mappings.Should().Contain(m => m.SourceClaim == "picture" && m.TargetClaim == "avatar_url");
    }

    [Fact]
    public void Merge_With_Null_Custom_Should_Return_Base()
    {
        var @base = OidcClaimMapping.Default;

        var merged = OidcClaimMapping.Merge(@base, null);

        merged.Mappings.Should().BeEquivalentTo(@base.Mappings);
    }

    [Fact]
    public void Merge_With_Null_Base_Should_Use_Default_And_Append_Custom()
    {
        var custom = new OidcClaimMapping
        {
            Mappings = new List<ClaimMapping>
            {
                new("picture", "avatar_url")
            }
        };

        var merged = OidcClaimMapping.Merge(null, custom);

        merged.Mappings.Should().HaveCount(5);
        merged.Mappings.Should().Contain(m => m.SourceClaim == "sub");
        merged.Mappings.Should().Contain(m => m.SourceClaim == "picture" && m.TargetClaim == "avatar_url");
    }

    [Fact]
    public void Merge_With_Both_Null_Should_Return_Default()
    {
        var merged = OidcClaimMapping.Merge(null, null);

        merged.Mappings.Should().BeEquivalentTo(OidcClaimMapping.Default.Mappings);
    }

    [Fact]
    public void Merge_Should_Be_Case_Insensitive_On_SourceClaim_When_Overriding()
    {
        var @base = OidcClaimMapping.Default;
        var custom = new OidcClaimMapping
        {
            Mappings = new List<ClaimMapping>
            {
                new("EMAIL", "mail")
            }
        };

        var merged = OidcClaimMapping.Merge(@base, custom);

        // EMAIL 与 email 视为相同 SourceClaim，自定义覆盖默认
        merged.Mappings.Should().Contain(m => m.SourceClaim == "EMAIL" && m.TargetClaim == "mail");
        merged.Mappings.Should().NotContain(m => m.SourceClaim == "email" && m.TargetClaim == "email");
    }

    [Fact]
    public void Mappings_Init_Default_Should_Be_Empty_List()
    {
        var mapping = new OidcClaimMapping();

        mapping.Mappings.Should().NotBeNull();
        mapping.Mappings.Should().BeEmpty();
    }
}
