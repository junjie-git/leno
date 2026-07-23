using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Domain.Tests.Aggregates;

/// <summary>
/// OAuthClient 聚合根单元测试（Identity BC，3.7 OAuth/SSO 通用化）。
/// 覆盖配置驱动创建、协议类型校验、DiscoveryUrl 校验、Scopes/ClaimMappings 加载与状态切换。
/// </summary>
public class OAuthClientTests
{
    private static readonly Guid ClientId = Guid.NewGuid();
    private const string Provider = "google";
    private const string ProviderTypeOidc = "Oidc";
    private const string DiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration";
    private const string ClientIdValue = "test-client-id";
    private const string EncryptedSecret = "encrypted-secret-cipher";
    private const string RedirectUri = "https://leno.local/callback";

    [Fact]
    public void Create_With_Valid_Oidc_Config_Should_Succeed()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            new[] { "openid", "email", "profile" },
            DiscoveryUrl);

        client.Id.Should().Be(ClientId);
        client.Provider.Should().Be("google");
        client.ProviderType.Should().Be("Oidc");
        client.DiscoveryUrl.Should().Be(DiscoveryUrl);
        client.ClientId.Should().Be(ClientIdValue);
        client.ClientSecret.Should().Be(EncryptedSecret);
        client.RedirectUri.Should().Be(RedirectUri);
        client.Scopes.Should().Equal("openid", "email", "profile");
        client.ClaimMappings.Should().BeEmpty();
        client.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Create_Should_Normalize_Provider_To_Lowercase()
    {
        var client = OAuthClient.Create(
            ClientId,
            "KeyCloak",
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        client.Provider.Should().Be("keycloak");
    }

    [Theory]
    [InlineData("oidc", "Oidc")]
    [InlineData("OIDC", "Oidc")]
    [InlineData("saml2", "Saml2")]
    [InlineData("SAML2", "Saml2")]
    [InlineData("google", "Google")]
    [InlineData("wechat", "WeChat")]
    public void Create_Should_Normalize_ProviderType_To_PascalCase(string input, string expected)
    {
        var discoveryUrl = expected == "Oidc" ? DiscoveryUrl : null;

        var client = OAuthClient.Create(
            ClientId,
            Provider,
            input,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            discoveryUrl);

        client.ProviderType.Should().Be(expected);
    }

    [Fact]
    public void Create_Oidc_Without_DiscoveryUrl_Should_Throw()
    {
        var act = () => OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            null);

        var ex = act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_DISCOVERY_URL_REQUIRED");
    }

    [Theory]
    [InlineData("ftp://example.com/.well-known/openid-configuration")]
    [InlineData("not-a-url")]
    public void Create_Oidc_With_Invalid_DiscoveryUrl_Should_Throw(string invalidUrl)
    {
        var act = () => OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            invalidUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_DISCOVERY_URL_FORMAT");
    }

    [Fact]
    public void Create_Saml2_Without_DiscoveryUrl_Should_Succeed()
    {
        var client = OAuthClient.Create(
            ClientId,
            "adfs",
            "Saml2",
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            null);

        client.ProviderType.Should().Be("Saml2");
        client.DiscoveryUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("OAuth")]
    [InlineData("Saml")]
    public void Create_With_Unsupported_ProviderType_Should_Throw(string providerType)
    {
        var act = () => OAuthClient.Create(
            ClientId,
            Provider,
            providerType,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_PROVIDER_TYPE_INVALID");
    }

    [Fact]
    public void Create_With_Empty_ProviderType_Should_Throw()
    {
        var act = () => OAuthClient.Create(
            ClientId,
            Provider,
            "",
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_PROVIDER_TYPE_EMPTY");
    }

    [Fact]
    public void Create_With_Empty_Provider_Should_Throw()
    {
        var act = () => OAuthClient.Create(
            ClientId,
            "",
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_PROVIDER_EMPTY");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("this_provider_name_is_way_too_long_for_the_limit_yes")]
    public void Create_With_Provider_Length_Out_Of_Range_Should_Throw(string provider)
    {
        var act = () => OAuthClient.Create(
            ClientId,
            provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_PROVIDER_LENGTH");
    }

    [Fact]
    public void Create_With_Empty_ClientId_Should_Throw()
    {
        var act = () => OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            "",
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_CLIENT_ID_EMPTY");
    }

    [Fact]
    public void Create_With_Empty_Secret_Should_Throw()
    {
        var act = () => OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            "",
            RedirectUri,
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_SECRET_EMPTY");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://leno.local/callback")]
    public void Create_With_Invalid_RedirectUri_Should_Throw(string redirectUri)
    {
        var act = () => OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            redirectUri,
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_REDIRECT_URI_FORMAT");
    }

    [Fact]
    public void Create_With_Empty_Guid_Should_Throw()
    {
        var act = () => OAuthClient.Create(
            Guid.Empty,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_ID_EMPTY");
    }

    [Fact]
    public void Create_Should_Trim_Scopes_And_Remove_Empty()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            new[] { "  openid  ", "", "  ", "email" },
            DiscoveryUrl);

        client.Scopes.Should().Equal("openid", "email");
    }

    [Fact]
    public void Create_With_Whitespace_Scope_Should_Be_Filtered_Out()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            new[] { "openid", "   " },
            DiscoveryUrl);

        client.Scopes.Should().Equal("openid");
    }

    [Fact]
    public void Create_With_ClaimMappings_Should_Preserve_Them()
    {
        var mappings = new List<ClaimMapping>
        {
            new("email", "mail"),
            new("picture", "avatar_url")
        };

        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl,
            mappings);

        client.ClaimMappings.Should().HaveCount(2);
        client.ClaimMappings.Should().Contain(m => m.SourceClaim == "email" && m.TargetClaim == "mail");
        client.ClaimMappings.Should().Contain(m => m.SourceClaim == "picture" && m.TargetClaim == "avatar_url");
    }

    [Fact]
    public void Create_With_Enabled_False_Should_Preserve_Status()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl,
            null,
            enabled: false);

        client.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_And_Disable_Should_Toggle_Enabled_Flag()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl,
            null,
            enabled: false);

        client.Enabled.Should().BeFalse();

        client.Enable();
        client.Enabled.Should().BeTrue();

        client.Disable();
        client.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Update_Should_Modify_Client_Parameters()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        var newMappings = new List<ClaimMapping> { new("email", "mail") };

        client.Update(
            "new-client-id",
            "new-secret",
            "https://leno.local/new-callback",
            new[] { "openid", "profile" },
            DiscoveryUrl,
            newMappings);

        client.ClientId.Should().Be("new-client-id");
        client.ClientSecret.Should().Be("new-secret");
        client.RedirectUri.Should().Be("https://leno.local/new-callback");
        client.Scopes.Should().Equal("openid", "profile");
        client.ClaimMappings.Should().ContainSingle().Which.SourceClaim.Should().Be("email");
    }

    [Fact]
    public void Update_With_Invalid_RedirectUri_Should_Throw()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        var act = () => client.Update(
            ClientIdValue,
            EncryptedSecret,
            "not-a-url",
            null,
            DiscoveryUrl);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_REDIRECT_URI_FORMAT");
    }

    [Fact]
    public void UpdateProviderType_Should_Change_Type_And_DiscoveryUrl()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        client.UpdateProviderType("Saml2", "https://idp.local/sso");

        client.ProviderType.Should().Be("Saml2");
        client.DiscoveryUrl.Should().Be("https://idp.local/sso");
    }

    [Fact]
    public void UpdateProviderType_To_Oidc_Without_DiscoveryUrl_Should_Throw()
    {
        var client = OAuthClient.Create(
            ClientId,
            "adfs",
            "Saml2",
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            "https://idp.local/sso");

        var act = () => client.UpdateProviderType("Oidc", null);

        act.Should().Throw<IdentityDomainException>()
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_DISCOVERY_URL_REQUIRED");
    }

    [Fact]
    public void Create_With_Null_Scopes_Should_Default_To_Empty_Array()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl);

        client.Scopes.Should().NotBeNull();
        client.Scopes.Should().BeEmpty();
    }

    [Fact]
    public void Create_With_Null_ClaimMappings_Should_Default_To_Empty_List()
    {
        var client = OAuthClient.Create(
            ClientId,
            Provider,
            ProviderTypeOidc,
            ClientIdValue,
            EncryptedSecret,
            RedirectUri,
            null,
            DiscoveryUrl,
            null);

        client.ClaimMappings.Should().NotBeNull();
        client.ClaimMappings.Should().BeEmpty();
    }
}
