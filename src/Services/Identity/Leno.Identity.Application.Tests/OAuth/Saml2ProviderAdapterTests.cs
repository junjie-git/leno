using System.Security.Claims;
using System.Text;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;
using Leno.Identity.Infrastructure.OAuth;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Tests.OAuth;

/// <summary>
/// Saml2ProviderAdapter 单元测试（Identity BC，3.7 OAuth/SSO 通用化）。
/// 覆盖 SAML AuthnRequest 构造、SAMLResponse 解析、断言时效校验与 claim 映射。
/// 由于 SAML2 协议适配器不依赖外部 HTTP（code/accessToken 即 SAMLResponse 字符串），
/// 测试直接传入原始 XML（适配器 DecodeSamlMessage 兼容以 '&lt;' 开头的原始 XML）。
/// </summary>
public class Saml2ProviderAdapterTests
{
    private static Saml2ProviderAdapter CreateAdapter()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler());
        var logger = Mock.Of<ILogger<Saml2ProviderAdapter>>();
        return new Saml2ProviderAdapter(httpClient, logger);
    }

    private static OAuthClient CreateSaml2Client(string? idpSsoUrl = "https://idp.local/sso")
    {
        return OAuthClient.Create(
            Guid.NewGuid(),
            "adfs",
            "Saml2",
            "sp-entity-id",
            "encrypted-secret",
            "https://leno.local/acs",
            null,
            idpSsoUrl);
    }

    private static string BuildSamlResponse(
        string nameId = "user@example.com",
        string statusCode = "urn:oasis:names:tc:SAML:2.0:status:Success",
        string? notBefore = null,
        string? notOnOrAfter = null,
        Dictionary<string, string>? attributes = null)
    {
        var conditionsAttrs = string.Empty;
        if (notBefore is not null || notOnOrAfter is not null)
        {
            var parts = new List<string>();
            if (notBefore is not null) parts.Add($"NotBefore=\"{notBefore}\"");
            if (notOnOrAfter is not null) parts.Add($"NotOnOrAfter=\"{notOnOrAfter}\"");
            conditionsAttrs = " " + string.Join(" ", parts);
        }

        var attributeXml = new StringBuilder();
        if (attributes is not null)
        {
            foreach (var kv in attributes)
            {
                attributeXml.AppendLine($"    <saml:Attribute Name=\"{kv.Key}\">");
                attributeXml.AppendLine($"      <saml:AttributeValue>{kv.Value}</saml:AttributeValue>");
                attributeXml.AppendLine($"    </saml:Attribute>");
            }
        }

        var attributeStatement = attributeXml.Length > 0
            ? $"<saml:AttributeStatement>\n{attributeXml}</saml:AttributeStatement>"
            : string.Empty;

        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
  <samlp:Status>
    <samlp:StatusCode Value="{statusCode}" />
  </samlp:Status>
  <saml:Assertion>
    <saml:Subject>
      <saml:NameID>{nameId}</saml:NameID>
    </saml:Subject>
    <saml:Conditions{conditionsAttrs} />
    {attributeStatement}
  </saml:Assertion>
</samlp:Response>
""";
    }

    [Fact]
    public void ProviderType_Should_Return_Saml2()
    {
        var adapter = CreateAdapter();

        adapter.ProviderType.Should().Be("Saml2");
    }

    [Fact]
    public void Constructor_With_Null_HttpClient_Should_Throw()
    {
        var act = () => new Saml2ProviderAdapter(null!, Mock.Of<ILogger<Saml2ProviderAdapter>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_With_Null_Logger_Should_Throw()
    {
        var act = () => new Saml2ProviderAdapter(new HttpClient(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_Should_Construct_SamlRequest_Url()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client("https://idp.local/sso");

        var result = await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/acs", "relay-state-123", CancellationToken.None);

        result.AuthorizationUri.Should().StartWith("https://idp.local/sso?");
        result.AuthorizationUri.Should().Contain("SAMLRequest=");
        result.AuthorizationUri.Should().Contain("RelayState=relay-state-123");
        result.State.Should().Be("relay-state-123");
        // SAML2 用 Request ID 作为 Nonce（防重放）
        result.Nonce.Should().StartWith("_");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_Should_Append_With_Amp_When_Url_Has_Query()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client("https://idp.local/sso?existing=1");

        var result = await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/acs", "state", CancellationToken.None);

        result.AuthorizationUri.Should().Contain("?existing=1&");
        result.AuthorizationUri.Should().Contain("SAMLRequest=");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Missing_DiscoveryUrl_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client(null);

        var act = async () => await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/acs", "s", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_IDP_SSO_URL_MISSING");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Empty_RedirectUri_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();

        var act = async () => await adapter.BuildAuthorizationUriAsync(client, "", "s", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_ACS_EMPTY");
    }

    [Fact]
    public async Task BuildAuthorizationUriAsync_With_Empty_State_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();

        var act = async () => await adapter.BuildAuthorizationUriAsync(client, "https://leno.local/acs", "", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_STATE_EMPTY");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_Should_Transparently_Pass_SamlResponse_As_Token()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var samlResponse = BuildSamlResponse();

        var token = await adapter.ExchangeCodeForTokenAsync(client, samlResponse, "https://leno.local/acs", CancellationToken.None);

        token.AccessToken.Should().Be(samlResponse);
        token.TokenType.Should().Be("SAML2");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_With_Non_Success_Status_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var samlResponse = BuildSamlResponse(statusCode: "urn:oasis:names:tc:SAML:2.0:status:Requester");

        var act = async () => await adapter.ExchangeCodeForTokenAsync(client, samlResponse, "https://leno.local/acs", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_RESPONSE_STATUS_FAILED");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_With_Empty_Code_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();

        var act = async () => await adapter.ExchangeCodeForTokenAsync(client, "", "https://leno.local/acs", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_RESPONSE_EMPTY");
    }

    [Fact]
    public async Task GetUserInfoAsync_Should_Extract_NameId_As_Sub_And_Attributes()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var samlResponse = BuildSamlResponse(
            nameId: "user@example.com",
            attributes: new Dictionary<string, string>
            {
                ["email"] = "user@example.com",
                ["name"] = "Test User"
            });

        var userInfo = await adapter.GetUserInfoAsync(client, samlResponse, CancellationToken.None);

        userInfo.Subject.Should().Be("user@example.com");
        userInfo.RawClaims["sub"].Should().Be("user@example.com");
        userInfo.RawClaims["email"].Should().Be("user@example.com");
        userInfo.RawClaims["name"].Should().Be("Test User");
    }

    [Fact]
    public async Task GetUserInfoAsync_Should_Map_Saml_Claim_Uris_To_Oidc_Style()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var samlResponse = BuildSamlResponse(
            nameId: "user@example.com",
            attributes: new Dictionary<string, string>
            {
                ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"] = "user@example.com",
                ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] = "Test User"
            });

        var userInfo = await adapter.GetUserInfoAsync(client, samlResponse, CancellationToken.None);

        // 兼容性映射应产生 OIDC 风格 key
        userInfo.RawClaims["email"].Should().Be("user@example.com");
        userInfo.RawClaims["name"].Should().Be("Test User");
    }

    [Fact]
    public async Task GetUserInfoAsync_With_Missing_Assertion_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var samlResponse = """
<?xml version="1.0"?>
<samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
  <samlp:Status><samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" /></samlp:Status>
</samlp:Response>
""";

        var act = async () => await adapter.GetUserInfoAsync(client, samlResponse, CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_ASSERTION_MISSING");
    }

    [Fact]
    public async Task GetUserInfoAsync_With_Missing_NameId_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var samlResponse = """
<?xml version="1.0"?>
<samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
  <samlp:Status><samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" /></samlp:Status>
  <saml:Assertion>
    <saml:Subject></saml:Subject>
  </saml:Assertion>
</samlp:Response>
""";

        var act = async () => await adapter.GetUserInfoAsync(client, samlResponse, CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_NAMEID_MISSING");
    }

    [Fact]
    public async Task GetUserInfoAsync_With_Expired_Assertion_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var samlResponse = BuildSamlResponse(
            notOnOrAfter: "2020-01-01T00:00:00Z");

        var act = async () => await adapter.GetUserInfoAsync(client, samlResponse, CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_ASSERTION_EXPIRED");
    }

    [Fact]
    public async Task GetUserInfoAsync_With_Not_Yet_Valid_Assertion_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var future = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var samlResponse = BuildSamlResponse(notBefore: future);

        var act = async () => await adapter.GetUserInfoAsync(client, samlResponse, CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_ASSERTION_NOT_YET_VALID");
    }

    [Fact]
    public async Task GetUserInfoAsync_With_Empty_Token_Should_Throw()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();

        var act = async () => await adapter.GetUserInfoAsync(client, "", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("SAML_RESPONSE_EMPTY");
    }

    [Fact]
    public async Task MapClaimsAsync_Should_Apply_Mapping_Rules()
    {
        var adapter = CreateAdapter();
        var userInfo = new UserInfoResponse
        {
            RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sub"] = "user-123",
                ["email"] = "user@example.com"
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
    public async Task MapClaimsAsync_Should_Use_Saml2_Authentication_Type()
    {
        var adapter = CreateAdapter();
        var userInfo = new UserInfoResponse
        {
            RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["sub"] = "u1" }
        };

        var principal = await adapter.MapClaimsAsync(userInfo, OidcClaimMapping.Default, CancellationToken.None);

        principal.Identity!.AuthenticationType.Should().Be("Saml2");
    }

    [Fact]
    public async Task MapClaimsAsync_With_Null_UserInfo_Should_Throw()
    {
        var adapter = CreateAdapter();

        var act = async () => await adapter.MapClaimsAsync(null!, OidcClaimMapping.Default, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Full_Saml2_Flow_Should_Produce_Valid_UserInfo()
    {
        var adapter = CreateAdapter();
        var client = CreateSaml2Client();
        var samlResponse = BuildSamlResponse(
            nameId: "user@example.com",
            attributes: new Dictionary<string, string>
            {
                ["email"] = "user@example.com",
                ["name"] = "Test User"
            });

        // ExchangeCodeForTokenAsync 透传 SAMLResponse 作为 AccessToken
        var token = await adapter.ExchangeCodeForTokenAsync(client, samlResponse, "https://leno.local/acs", CancellationToken.None);
        // GetUserInfoAsync 解析 SAMLResponse
        var userInfo = await adapter.GetUserInfoAsync(client, token.AccessToken, CancellationToken.None);
        // MapClaimsAsync 映射为 ClaimsPrincipal
        var principal = await adapter.MapClaimsAsync(userInfo, OidcClaimMapping.Default, CancellationToken.None);

        userInfo.Subject.Should().Be("user@example.com");
        principal.FindFirst("sub")!.Value.Should().Be("user@example.com");
        principal.FindFirst("email")!.Value.Should().Be("user@example.com");
        principal.FindFirst("name")!.Value.Should().Be("Test User");
    }
}
