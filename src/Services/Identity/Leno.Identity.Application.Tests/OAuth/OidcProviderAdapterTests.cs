using System.Net;
using System.Security.Claims;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;
using Leno.Identity.Infrastructure.OAuth;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Tests.OAuth;

/// <summary>
/// OidcProviderAdapter 单元测试（Identity BC，3.7 OAuth/SSO 通用化）。
/// 覆盖 claim 映射、授权 URL 构造、授权码交换与 userinfo 拉取（通过 FakeHttpMessageHandler 模拟 IdP）。
/// 每个测试使用唯一 discovery URL，避免适配器静态 discovery 缓存跨测试污染。
/// </summary>
public class OidcProviderAdapterTests
{
    private static int _urlCounter;

    private static string NextDiscoveryUrl()
    {
        // 唯一 URL 避免静态 discovery 缓存命中其它测试的响应
        var id = Interlocked.Increment(ref _urlCounter);
        return $"https://idp{Guid.NewGuid():N}{id}.local/.well-known/openid-configuration";
    }

    private static OidcProviderAdapter CreateAdapter(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var logger = Mock.Of<ILogger<OidcProviderAdapter>>();
        return new OidcProviderAdapter(httpClient, logger);
    }

    private static OAuthClient CreateOidcClient(string discoveryUrl)
    {
        return OAuthClient.Create(
            Guid.NewGuid(),
            "test-idp",
            "Oidc",
            "test-client-id",
            "encrypted-secret",
            "https://leno.local/callback",
            new[] { "openid", "email", "profile" },
            discoveryUrl);
    }

    private static void RegisterDiscovery(FakeHttpMessageHandler handler, string discoveryUrl)
    {
        handler.Register(discoveryUrl, HttpStatusCode.OK, $$"""
        {
            "issuer": "https://idp.local",
            "authorization_endpoint": "https://idp.local/authorize",
            "token_endpoint": "https://idp.local/token",
            "userinfo_endpoint": "https://idp.local/userinfo"
        }
        """);
    }

    [Fact]
    public void ProviderType_Should_Return_Oidc()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());

        adapter.ProviderType.Should().Be("Oidc");
    }

    [Fact]
    public void Constructor_With_Null_HttpClient_Should_Throw()
    {
        var act = () => new OidcProviderAdapter(null!, Mock.Of<ILogger<OidcProviderAdapter>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_With_Null_Logger_Should_Throw()
    {
        var act = () => new OidcProviderAdapter(new HttpClient(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task MapClaimsAsync_Should_Apply_Mapping_Rules()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());
        var userInfo = new UserInfoResponse
        {
            RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sub"] = "user-123",
                ["email"] = "user@example.com",
                ["name"] = "Test User"
            }
        };
        var mapping = new OidcClaimMapping
        {
            Mappings = new List<ClaimMapping>
            {
                new("sub", "sub"),
                new("email", "mail")
            }
        };

        var principal = await adapter.MapClaimsAsync(userInfo, mapping, CancellationToken.None);

        principal.FindFirst("sub")!.Value.Should().Be("user-123");
        principal.FindFirst("mail")!.Value.Should().Be("user@example.com");
    }

    [Fact]
    public async Task MapClaimsAsync_Should_Pass_Through_Unmapped_Claims()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());
        var userInfo = new UserInfoResponse
        {
            RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sub"] = "user-123",
                ["picture"] = "https://idp.local/avatar.png"
            }
        };
        var mapping = new OidcClaimMapping
        {
            Mappings = new List<ClaimMapping> { new("sub", "sub") }
        };

        var principal = await adapter.MapClaimsAsync(userInfo, mapping, CancellationToken.None);

        // sub 已映射，picture 未在映射规则中应透传
        principal.FindFirst("sub")!.Value.Should().Be("user-123");
        principal.FindFirst("picture")!.Value.Should().Be("https://idp.local/avatar.png");
    }

    [Fact]
    public async Task MapClaimsAsync_With_Null_UserInfo_Should_Throw()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());

        var act = async () => await adapter.MapClaimsAsync(null!, OidcClaimMapping.Default, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task MapClaimsAsync_With_Null_Mapping_Should_Throw()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());
        var userInfo = new UserInfoResponse();

        var act = async () => await adapter.MapClaimsAsync(userInfo, null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task MapClaimsAsync_Should_Use_Oidc_Authentication_Type()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());
        var userInfo = new UserInfoResponse
        {
            RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["sub"] = "u1" }
        };

        var principal = await adapter.MapClaimsAsync(userInfo, OidcClaimMapping.Default, CancellationToken.None);

        principal.Identity!.AuthenticationType.Should().Be("Oidc");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_Should_Construct_Url_With_Standard_Params()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        RegisterDiscovery(handler, discoveryUrl);
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var result = await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/callback", "state-abc", CancellationToken.None);

        result.AuthorizationUri.Should().StartWith("https://idp.local/authorize?");
        result.AuthorizationUri.Should().Contain("response_type=code");
        result.AuthorizationUri.Should().Contain($"client_id={Uri.EscapeDataString(client.ClientId)}");
        result.AuthorizationUri.Should().Contain("redirect_uri=");
        result.AuthorizationUri.Should().Contain("scope=openid%20email%20profile");
        result.AuthorizationUri.Should().Contain("state=state-abc");
        result.AuthorizationUri.Should().Contain("nonce=");
        result.State.Should().Be("state-abc");
        result.Nonce.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_Should_Use_Default_Scopes_When_Client_Scopes_Empty()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        RegisterDiscovery(handler, discoveryUrl);
        var adapter = CreateAdapter(handler);
        var client = OAuthClient.Create(
            Guid.NewGuid(),
            "test-idp",
            "Oidc",
            "cid",
            "secret",
            "https://leno.local/callback",
            null,
            discoveryUrl);

        var result = await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/callback", "s", CancellationToken.None);

        result.AuthorizationUri.Should().Contain("scope=openid%20profile%20email");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Null_Client_Should_Throw()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());

        var act = async () => await adapter.BuildAuthorizationUriAsync(null!, "https://leno.local/callback", "s", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Empty_RedirectUri_Should_Throw()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());
        var client = CreateOidcClient(NextDiscoveryUrl());

        var act = async () => await adapter.BuildAuthorizationUriAsync(client, "", "s", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_REDIRECT_URI_EMPTY");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Empty_State_Should_Throw()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());
        var client = CreateOidcClient(NextDiscoveryUrl());

        var act = async () => await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/callback", "", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_STATE_EMPTY");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_Should_Return_Token_Response()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        RegisterDiscovery(handler, discoveryUrl);
        handler.Register("token", HttpStatusCode.OK, """
        {
            "access_token": "at-xyz",
            "token_type": "Bearer",
            "expires_in": 3600,
            "id_token": "id-jwt",
            "refresh_token": "rt-abc",
            "scope": "openid email"
        }
        """);
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var token = await adapter.ExchangeCodeForTokenAsync(client, "auth-code", "https://leno.local/callback", CancellationToken.None);

        token.AccessToken.Should().Be("at-xyz");
        token.TokenType.Should().Be("Bearer");
        token.ExpiresIn.Should().Be(3600);
        token.IdToken.Should().Be("id-jwt");
        token.RefreshToken.Should().Be("rt-abc");
        token.Scope.Should().Be("openid email");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_With_Failed_Status_Should_Throw()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        RegisterDiscovery(handler, discoveryUrl);
        handler.Register("token", HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var act = async () => await adapter.ExchangeCodeForTokenAsync(client, "bad-code", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_TOKEN_EXCHANGE_FAILED");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_Without_AccessToken_Should_Throw()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        RegisterDiscovery(handler, discoveryUrl);
        handler.Register("token", HttpStatusCode.OK, """{"token_type":"Bearer"}""");
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var act = async () => await adapter.ExchangeCodeForTokenAsync(client, "code", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_TOKEN_EMPTY");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_With_Empty_Code_Should_Throw()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());
        var client = CreateOidcClient(NextDiscoveryUrl());

        var act = async () => await adapter.ExchangeCodeForTokenAsync(client, "", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_CODE_EMPTY");
    }

    [Fact]
    public async Task GetUserInfoAsync_Should_Parse_All_Scalar_Claims()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        RegisterDiscovery(handler, discoveryUrl);
        handler.Register("userinfo", HttpStatusCode.OK, """
        {
            "sub": "user-123",
            "email": "user@example.com",
            "name": "Test User",
            "email_verified": true,
            "age": 30
        }
        """);
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var userInfo = await adapter.GetUserInfoAsync(client, "at-xyz", CancellationToken.None);

        userInfo.Subject.Should().Be("user-123");
        userInfo.RawClaims["email"].Should().Be("user@example.com");
        userInfo.RawClaims["name"].Should().Be("Test User");
        userInfo.RawClaims["email_verified"].Should().Be("true");
        userInfo.RawClaims["age"].Should().Be("30");
        userInfo.Endpoint.Should().Be("https://idp.local/userinfo");
    }

    [Fact]
    public async Task GetUserInfoAsync_Without_Sub_Should_Throw()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        RegisterDiscovery(handler, discoveryUrl);
        handler.Register("userinfo", HttpStatusCode.OK, """{"email":"user@example.com"}""");
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var act = async () => await adapter.GetUserInfoAsync(client, "at-xyz", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_USER_ID_EMPTY");
    }

    [Fact]
    public async Task GetUserInfoAsync_With_Failed_Status_Should_Throw()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        RegisterDiscovery(handler, discoveryUrl);
        handler.Register("userinfo", HttpStatusCode.Unauthorized, "unauthorized");
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var act = async () => await adapter.GetUserInfoAsync(client, "bad-token", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_USERINFO_FAILED");
    }

    [Fact]
    public async Task GetUserInfoAsync_With_Empty_Token_Should_Throw()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler());
        var client = CreateOidcClient(NextDiscoveryUrl());

        var act = async () => await adapter.GetUserInfoAsync(client, "", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_ACCESS_TOKEN_EMPTY");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Missing_DiscoveryUrl_Should_Throw()
    {
        var handler = new FakeHttpMessageHandler();
        var adapter = CreateAdapter(handler);
        // Oidc 聚合强制要求 DiscoveryUrl，此处用 Saml2 类型创建 DiscoveryUrl 为空的客户端，
        // 传给 Oidc 适配器以触发适配器内部的防御性校验（OAUTH_DISCOVERY_URL_MISSING）。
        var client = OAuthClient.Create(
            Guid.NewGuid(),
            "idp-no-discovery",
            "Saml2",
            "cid",
            "secret",
            "https://leno.local/callback",
            null,
            null);

        var act = async () => await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/callback", "s", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_DISCOVERY_URL_MISSING");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Failed_Discovery_Should_Throw()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        handler.Register(discoveryUrl, HttpStatusCode.InternalServerError, "idp down");
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var act = async () => await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/callback", "s", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_DISCOVERY_FAILED");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Incomplete_Discovery_Should_Throw()
    {
        var handler = new FakeHttpMessageHandler();
        var discoveryUrl = NextDiscoveryUrl();
        // 缺少 userinfo_endpoint
        handler.Register(discoveryUrl, HttpStatusCode.OK, """
        {
            "authorization_endpoint": "https://idp.local/authorize",
            "token_endpoint": "https://idp.local/token"
        }
        """);
        var adapter = CreateAdapter(handler);
        var client = CreateOidcClient(discoveryUrl);

        var act = async () => await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/callback", "s", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_DISCOVERY_INVALID");
    }
}
