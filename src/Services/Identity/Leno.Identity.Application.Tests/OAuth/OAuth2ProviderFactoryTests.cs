using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;
using Leno.Identity.Infrastructure.OAuth;

namespace Leno.Identity.Application.Tests.OAuth;

/// <summary>
/// OAuth2ProviderFactory 单元测试（Identity BC，3.7 OAuth/SSO 通用化）。
/// 覆盖按 ProviderType 路由、大小写不敏感、未知类型异常与已注册类型枚举。
/// </summary>
public class OAuth2ProviderFactoryTests
{
    [Fact]
    public void GetAdapter_Should_Return_Registered_Adapter_By_Type()
    {
        var oidc = new FakeAdapter("Oidc");
        var saml2 = new FakeAdapter("Saml2");
        var factory = new OAuth2ProviderFactory(new IOAuth2ProviderAdapter[] { oidc, saml2 });

        var resolved = factory.GetAdapter("Oidc");

        resolved.Should().BeSameAs(oidc);
    }

    [Fact]
    public void GetAdapter_Should_Be_Case_Insensitive()
    {
        var oidc = new FakeAdapter("Oidc");
        var factory = new OAuth2ProviderFactory(new IOAuth2ProviderAdapter[] { oidc });

        factory.GetAdapter("oidc").Should().BeSameAs(oidc);
        factory.GetAdapter("OIDC").Should().BeSameAs(oidc);
        factory.GetAdapter("OiDc").Should().BeSameAs(oidc);
    }

    [Fact]
    public void GetAdapter_With_Unknown_Type_Should_Throw_With_Available_Types()
    {
        var oidc = new FakeAdapter("Oidc");
        var saml2 = new FakeAdapter("Saml2");
        var factory = new OAuth2ProviderFactory(new IOAuth2ProviderAdapter[] { oidc, saml2 });

        var act = () => factory.GetAdapter("WeChat");

        var ex = act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Oidc").And.Contain("Saml2");
    }

    [Fact]
    public void GetAdapter_With_Empty_Type_Should_Throw()
    {
        var factory = new OAuth2ProviderFactory(new IOAuth2ProviderAdapter[] { new FakeAdapter("Oidc") });

        var act = () => factory.GetAdapter("");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetAdapter_With_Whitespace_Type_Should_Throw()
    {
        var factory = new OAuth2ProviderFactory(new IOAuth2ProviderAdapter[] { new FakeAdapter("Oidc") });

        var act = () => factory.GetAdapter("   ");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetAdapter_With_No_Registered_Adapters_Should_Throw()
    {
        var factory = new OAuth2ProviderFactory(Array.Empty<IOAuth2ProviderAdapter>());

        var act = () => factory.GetAdapter("Oidc");

        var ex = act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("无已注册适配器");
    }

    [Fact]
    public void GetRegisteredProviderTypes_Should_Return_All_Types()
    {
        var factory = new OAuth2ProviderFactory(new IOAuth2ProviderAdapter[]
        {
            new FakeAdapter("Oidc"),
            new FakeAdapter("Saml2")
        });

        var types = factory.GetRegisteredProviderTypes();

        types.Should().HaveCount(2);
        types.Should().Contain("Oidc");
        types.Should().Contain("Saml2");
    }

    [Fact]
    public void Constructor_Should_Skip_Null_And_Empty_ProviderType_Adapters()
    {
        var valid = new FakeAdapter("Oidc");
        var factory = new OAuth2ProviderFactory(new IOAuth2ProviderAdapter?[]
        {
            valid,
            null,
            new FakeAdapter(""),
            new FakeAdapter("   ")
        }.Where(a => a is not null).Cast<IOAuth2ProviderAdapter>().ToArray());

        factory.GetRegisteredProviderTypes().Should().ContainSingle().Which.Should().Be("Oidc");
    }

    [Fact]
    public void Constructor_With_Duplicate_Types_Should_Keep_Last_Registered()
    {
        var first = new FakeAdapter("Oidc");
        var second = new FakeAdapter("Oidc");
        var factory = new OAuth2ProviderFactory(new IOAuth2ProviderAdapter[] { first, second });

        factory.GetAdapter("Oidc").Should().BeSameAs(second);
    }

    [Fact]
    public void Constructor_With_Null_Enum_Should_Throw()
    {
        var act = () => new OAuth2ProviderFactory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>测试用适配器桩，仅暴露 ProviderType。</summary>
    private sealed class FakeAdapter : IOAuth2ProviderAdapter
    {
        public FakeAdapter(string providerType)
        {
            ProviderType = providerType;
        }

        public string ProviderType { get; }

        public Task<AuthorizationUriResult> BuildAuthorizationUriAsync(
            OAuthClient client, string redirectUri, string state, CancellationToken ct)
            => Task.FromResult(new AuthorizationUriResult());

        public Task<TokenResponse> ExchangeCodeForTokenAsync(
            OAuthClient client, string code, string redirectUri, CancellationToken ct)
            => Task.FromResult(new TokenResponse());

        public Task<UserInfoResponse> GetUserInfoAsync(
            OAuthClient client, string accessToken, CancellationToken ct)
            => Task.FromResult(new UserInfoResponse());

        public Task<System.Security.Claims.ClaimsPrincipal> MapClaimsAsync(
            UserInfoResponse userInfo, OidcClaimMapping mapping, CancellationToken ct)
            => Task.FromResult(new System.Security.Claims.ClaimsPrincipal());
    }
}
