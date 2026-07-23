namespace Leno.Identity.Domain.ValueObjects;

/// <summary>
/// Claim 映射规则值对象（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 定义从第三方 IdP 返回的 source claim 到目标 claim 的映射关系，
/// 由 <c>IOAuth2ProviderAdapter.MapClaimsAsync</c> 在构造 <see cref="System.Security.Claims.ClaimsPrincipal"/> 时使用。
/// </para>
/// <para>
/// 不可变记录，相等性按 <see cref="SourceClaim"/> 与 <see cref="TargetClaim"/> 字段比较。
/// </para>
/// </summary>
public sealed record ClaimMapping(string SourceClaim, string TargetClaim);
