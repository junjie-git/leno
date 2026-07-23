using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Domain.Services;

/// <summary>
/// OIDC Claim 映射规则集合（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 承载一组 <see cref="ClaimMapping"/>，由 <c>IOAuth2ProviderAdapter.MapClaimsAsync</c> 在
/// 将 IdP 返回的 userinfo claim 转换为 <see cref="System.Security.Claims.ClaimsPrincipal"/> 时使用。
/// </para>
/// <para>
/// 通过 <see cref="Default"/> 提供标准 OIDC claim 的默认映射；OAuthClient 聚合可携带自定义映射覆盖默认行为。
/// </para>
/// </summary>
public sealed class OidcClaimMapping
{
    /// <summary>
    /// 映射规则集合。MapClaimsAsync 会按集合顺序处理，相同 SourceClaim 多次出现时后者覆盖前者。
    /// </summary>
    public List<ClaimMapping> Mappings { get; init; } = new();

    /// <summary>
    /// 默认 OIDC claim 映射：
    /// <list type="bullet">
    /// <item>sub → sub</item>
    /// <item>email → email</item>
    /// <item>name → name</item>
    /// <item>preferred_username → preferred_username</item>
    /// </list>
    /// </summary>
    public static OidcClaimMapping Default => new()
    {
        Mappings = new List<ClaimMapping>
        {
            new("sub", "sub"),
            new("email", "email"),
            new("name", "name"),
            new("preferred_username", "preferred_username")
        }
    };

    /// <summary>
    /// 合并两组映射规则：<paramref name="custom"/> 中的规则优先于 <paramref name="base"/> 中相同 SourceClaim 的规则。
    /// 用于 OAuthClient 自定义映射与默认映射的组合场景。
    /// </summary>
    public static OidcClaimMapping Merge(OidcClaimMapping? @base, OidcClaimMapping? custom)
    {
        var baseMappings = @base?.Mappings ?? Default.Mappings;
        var customMappings = custom?.Mappings ?? new List<ClaimMapping>();

        var merged = baseMappings
            .Where(b => customMappings.All(c => !string.Equals(c.SourceClaim, b.SourceClaim, StringComparison.OrdinalIgnoreCase)))
            .Concat(customMappings)
            .ToList();

        return new OidcClaimMapping { Mappings = merged };
    }
}
