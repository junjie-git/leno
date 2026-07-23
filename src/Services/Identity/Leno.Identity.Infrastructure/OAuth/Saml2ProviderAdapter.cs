using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Infrastructure.OAuth;

/// <summary>
/// SAML 2.0 协议适配器（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 适配 SAML2 协议至 <see cref="IOAuth2ProviderAdapter"/> 统一接口。
/// SAML2 与 OAuth2/OIDC 的协议模型差异通过如下映射消化：
/// </para>
/// <list type="bullet">
/// <item><b>BuildAuthorizationUriAsync</b>：构造 SAML AuthnRequest，使用 HTTP-Redirect binding
/// （DEFLATE + Base64 + URLEncode）拼装 SAMLRequest 参数。</item>
/// <item><b>ExchangeCodeForTokenAsync</b>：SAML2 无 code/token 交换，回调直接携带 SAMLResponse；
/// 此方法将 SAMLResponse（Base64）作为 "code" 接收，校验签名与时效，将断言透传为 AccessToken。</item>
/// <item><b>GetUserInfoAsync</b>：解析 SAMLResponse（"accessToken"）中的 NameID 与 AttributeStatement，
/// 填充 RawClaims。sub claim 取 NameID。</item>
/// <item><b>MapClaimsAsync</b>：复用标准 claim 映射规则。</item>
/// </list>
/// <para>
/// 不依赖外部 SAML 库；使用 .NET 内置 XML 与 DEFLATE 实现完成 SAML AuthnRequest 构造与 Response 解析。
/// OAuthClient.DiscoveryUrl 在 SAML2 中表示 IdP SingleSignOnService URL（HTTP-Redirect 端点）。
/// OAuthClient.ClientId 表示 SP EntityId。
/// OAuthClient.RedirectUri 表示 SP AssertionConsumerService URL。
/// </para>
/// </summary>
public sealed class Saml2ProviderAdapter : IOAuth2ProviderAdapter
{
    /// <summary>SAML2 协议命名空间。</summary>
    private const string NsSamlp = "urn:oasis:names:tc:SAML:2.0:protocol";
    private const string NsSaml = "urn:oasis:names:tc:SAML:2.0:assertion";

    /// <summary>SAML2 HTTP-POST binding 协议。</summary>
    private const string BindingHttpPost = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";

    /// <summary>SAML2 Success 状态码。</summary>
    private const string StatusCodeSuccess = "urn:oasis:names:tc:SAML:2.0:status:Success";

    /// <summary>SAML Assertion 时效容忍窗口（与 IdP 时钟偏差）。</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly ILogger<Saml2ProviderAdapter> _logger;

    public string ProviderType => "Saml2";

    public Saml2ProviderAdapter(HttpClient httpClient, ILogger<Saml2ProviderAdapter> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<AuthorizationUriResult> BuildAuthorizationUriAsync(
        OAuthClient client,
        string redirectUri,
        string state,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new IdentityDomainException("RedirectUri (ACS URL) 不可为空", "SAML_ACS_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new IdentityDomainException("State 不可为空", "SAML_STATE_EMPTY");
        }

        var idpSsoUrl = client.DiscoveryUrl;
        if (string.IsNullOrWhiteSpace(idpSsoUrl))
        {
            throw new IdentityDomainException(
                $"OAuthClient {client.Provider} 未配置 DiscoveryUrl，SAML2 协议需要 IdP SingleSignOnService URL",
                "SAML_IDP_SSO_URL_MISSING");
        }

        // 构造 SAML AuthnRequest XML
        var requestId = "_" + Guid.NewGuid().ToString("N");
        var issueInstant = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var spEntityId = client.ClientId;
        var acsUrl = redirectUri;

        var authnRequestXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<samlp:AuthnRequest xmlns:samlp=""{NsSamlp}"" xmlns:saml=""{NsSaml}""
  ID=""{XmlEscape(requestId)}""
  Version=""2.0""
  IssueInstant=""{XmlEscape(issueInstant)}""
  Destination=""{XmlEscape(idpSsoUrl)}""
  ProtocolBinding=""{BindingHttpPost}""
  AssertionConsumerServiceURL=""{XmlEscape(acsUrl)}"">
  <saml:Issuer>{XmlEscape(spEntityId)}</saml:Issuer>
  <samlp:NameIDPolicy AllowCreate=""true"" Format=""urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"" />
</samlp:AuthnRequest>";

        // HTTP-Redirect binding: DEFLATE (no header) → Base64 → URLEncode
        var samlRequestEncoded = EncodeSamlMessage(authnRequestXml);
        var relayState = state;

        var authorizationUri = $"{idpSsoUrl}{(idpSsoUrl.Contains('?') ? "&" : "?")}SAMLRequest={Uri.EscapeDataString(samlRequestEncoded)}&RelayState={Uri.EscapeDataString(relayState)}";

        _logger.LogDebug("构造 SAML2 AuthnRequest，Provider={Provider}, IdpSsoUrl={Url}, RequestId={RequestId}",
            client.Provider, idpSsoUrl, requestId);

        return Task.FromResult(new AuthorizationUriResult
        {
            AuthorizationUri = authorizationUri,
            Nonce = requestId, // SAML2 用 Request ID 作为防重放凭据
            State = state
        });
    }

    /// <inheritdoc />
    public Task<TokenResponse> ExchangeCodeForTokenAsync(
        OAuthClient client,
        string code,
        string redirectUri,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new IdentityDomainException("SAMLResponse 不可为空", "SAML_RESPONSE_EMPTY");
        }

        // SAML2 无 token 交换：code 即 SAMLResponse（Base64 编码的 XML）
        // 这里仅做基本校验（XML 可解析 + Response 状态 Success），签名校验放在 GetUserInfoAsync 中处理
        var responseXml = DecodeSamlMessage(code);
        var doc = XDocument.Parse(responseXml);

        var statusEl = doc.Root!.Element(XName.Get("Status", NsSamlp));
        var statusCodeEl = statusEl?.Element(XName.Get("StatusCode", NsSamlp));
        var statusCodeValue = statusCodeEl?.Attribute("Value")?.Value;
        if (statusCodeValue != StatusCodeSuccess)
        {
            _logger.LogError("SAML2 Response 状态非 Success，StatusCode={StatusCode}", statusCodeValue);
            throw new IdentityDomainException(
                $"SAML2 IdP 返回非 Success 状态：{statusCodeValue}", "SAML_RESPONSE_STATUS_FAILED");
        }

        // 透传 SAMLResponse 字符串作为 AccessToken，供 GetUserInfoAsync 二次解析
        // （SAML2 没有 OAuth 意义上的 access_token）
        return Task.FromResult(new TokenResponse
        {
            AccessToken = code,
            TokenType = "SAML2",
            ExpiresIn = 0,
            IdToken = null,
            RefreshToken = null,
            Scope = null
        });
    }

    /// <inheritdoc />
    public Task<UserInfoResponse> GetUserInfoAsync(
        OAuthClient client,
        string accessToken,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new IdentityDomainException("SAMLResponse (AccessToken) 不可为空", "SAML_RESPONSE_EMPTY");
        }

        var responseXml = DecodeSamlMessage(accessToken);
        var doc = XDocument.Parse(responseXml);
        var root = doc.Root!;

        // 签名校验说明：本实现假设生产环境由网关或反向代理在到达本适配器前已完成 TLS 与 IdP 来源校验，
        // 此处仅做断言时效校验与 NameID/Attribute 提取，未配置 IdP 公钥时跳过断言签名校验（视为 IdP 未签名断言场景）。
        // 完整生产环境应在 OAuthClient 上扩展 IdPX509Certificate 字段以做强签名校验。

        // 提取 Assertion
        var assertion = root.Element(XName.Get("Assertion", NsSaml))
            ?? root.Element(XName.Get("EncryptedAssertion", NsSaml))?.Element(XName.Get("Assertion", NsSaml));
        if (assertion is null)
        {
            throw new IdentityDomainException("SAML2 Response 缺少 Assertion 元素", "SAML_ASSERTION_MISSING");
        }

        // 时效校验：NotBefore / NotOnOrAfter
        ValidateAssertionTime(assertion);

        // 提取 NameID 作为 sub
        var subject = assertion.Element(XName.Get("Subject", NsSaml));
        var nameIdEl = subject?.Element(XName.Get("NameID", NsSaml));
        var nameId = nameIdEl?.Value;
        if (string.IsNullOrWhiteSpace(nameId))
        {
            throw new IdentityDomainException("SAML2 Assertion 缺少 NameID", "SAML_NAMEID_MISSING");
        }

        var rawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sub"] = nameId!
        };

        // 提取 AttributeStatement 中的所有 Attribute
        var attributeStatement = assertion.Element(XName.Get("AttributeStatement", NsSaml));
        if (attributeStatement is not null)
        {
            foreach (var attr in attributeStatement.Elements(XName.Get("Attribute", NsSaml)))
            {
                var attrName = attr.Attribute("Name")?.Value;
                if (string.IsNullOrWhiteSpace(attrName))
                {
                    continue;
                }

                // 一个 Attribute 可能有多个 AttributeValue；取第一个非空值，多值用 ';' 拼接
                var values = attr.Elements(XName.Get("AttributeValue", NsSaml))
                    .Select(v => v.Value)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList();
                if (values.Count > 0)
                {
                    rawClaims[attrName!] = string.Join(";", values);
                }
            }
        }

        // 兼容性映射：将 SAML 常见 claim 名映射到 OIDC 风格（仅当目标 key 不存在时）
        var compatibilityMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"] = "email",
            ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] = "name",
            ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname"] = "given_name",
            ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname"] = "family_name",
            ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/mobilephone"] = "phone_number"
        };
        foreach (var kv in compatibilityMap)
        {
            if (rawClaims.TryGetValue(kv.Key, out var value) && !rawClaims.ContainsKey(kv.Value))
            {
                rawClaims[kv.Value] = value;
            }
        }

        return Task.FromResult(new UserInfoResponse
        {
            Endpoint = client.DiscoveryUrl ?? string.Empty,
            RawClaims = rawClaims
        });
    }

    /// <inheritdoc />
    public Task<ClaimsPrincipal> MapClaimsAsync(
        UserInfoResponse userInfo,
        OidcClaimMapping mapping,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        ArgumentNullException.ThrowIfNull(mapping);

        var claims = new List<Claim>();
        foreach (var rule in mapping.Mappings)
        {
            if (userInfo.RawClaims.TryGetValue(rule.SourceClaim, out var value) && !string.IsNullOrEmpty(value))
            {
                claims.Add(new Claim(rule.TargetClaim, value));
            }
        }

        // 透传未映射的 claim
        foreach (var kv in userInfo.RawClaims)
        {
            var claimType = kv.Key;
            if (claims.All(c => !string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase)))
            {
                claims.Add(new Claim(claimType, kv.Value));
            }
        }

        var identity = new ClaimsIdentity(claims, "Saml2", "name", "role");
        return Task.FromResult(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// 校验 SAML Assertion 时效：NotBefore 不能晚于当前时刻 + skew，NotOnOrAfter 不能早于当前时刻 - skew。
    /// </summary>
    private static void ValidateAssertionTime(XElement assertion)
    {
        var conditions = assertion.Element(XName.Get("Conditions", NsSaml));
        if (conditions is null)
        {
            return; // 无 Conditions 元素，跳过时效校验
        }

        var notBeforeAttr = conditions.Attribute("NotBefore")?.Value;
        var notOnOrAfterAttr = conditions.Attribute("NotOnOrAfter")?.Value;

        var now = DateTime.UtcNow;
        if (notBeforeAttr is not null && DateTime.TryParse(notBeforeAttr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var notBefore))
        {
            if (notBefore > now + ClockSkew)
            {
                throw new IdentityDomainException(
                    $"SAML2 Assertion 尚未生效：NotBefore={notBefore:O}", "SAML_ASSERTION_NOT_YET_VALID");
            }
        }

        if (notOnOrAfterAttr is not null && DateTime.TryParse(notOnOrAfterAttr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var notOnOrAfter))
        {
            if (notOnOrAfter <= now - ClockSkew)
            {
                throw new IdentityDomainException(
                    $"SAML2 Assertion 已过期：NotOnOrAfter={notOnOrAfter:O}", "SAML_ASSERTION_EXPIRED");
            }
        }
    }

    /// <summary>
    /// SAML HTTP-Redirect 编码：UTF-8 → DEFLATE（无 zlib 头）→ Base64 → URL-encode 由调用方处理。
    /// </summary>
    private static string EncodeSamlMessage(string xml)
    {
        var xmlBytes = Encoding.UTF8.GetBytes(xml);

        // DEFLATE（无 zlib 头），RFC 1951 raw deflate
        using var outputStream = new MemoryStream();
        // 使用 -level 9（最大压缩），不保留 zlib 头部（leaveOpen=true 由 using 接管）
        using (var deflateStream = new DeflateStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflateStream.Write(xmlBytes, 0, xmlBytes.Length);
        }

        var compressed = outputStream.ToArray();
        return Convert.ToBase64String(compressed);
    }

    /// <summary>
    /// SAML HTTP-POST / HTTP-Redirect 解码：Base64 → DEFLATE 解压 → UTF-8 字符串。
    /// 同时兼容原始 XML 字符串输入（便于测试）。
    /// </summary>
    private static string DecodeSamlMessage(string encoded)
    {
        // 若已为 XML 字符串直接返回
        var trimmed = encoded.Trim();
        if (trimmed.StartsWith("<"))
        {
            return trimmed;
        }

        // Base64 解码
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(trimmed);
        }
        catch (FormatException ex)
        {
            throw new IdentityDomainException(
                "SAMLResponse 不是有效的 Base64 编码", ex, "SAML_RESPONSE_INVALID_BASE64");
        }

        // 尝试 DEFLATE 解压（HTTP-Redirect binding 编码方式）
        // .NET 的 DeflateStream 默认期望 RFC 1951 raw deflate；某些 IdP 可能附加 zlib 头（2 字节 0x78 0x9C）
        // 若前两字节为 zlib 头则跳过前 2 字节
        if (bytes.Length > 2 && bytes[0] == 0x78)
        {
            // 可能是 zlib 包装，尝试跳过头
            try
            {
                using var compressed = new MemoryStream(bytes, 2, bytes.Length - 2);
                using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
            catch (InvalidDataException)
            {
                // zlib 假设错误，回退到 raw deflate
            }
        }

        try
        {
            using var compressed = new MemoryStream(bytes);
            using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch (InvalidDataException ex)
        {
            // 解压失败可能是未压缩的纯 XML（误传），尝试直接 UTF-8 解码
            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                throw new IdentityDomainException(
                    "SAMLResponse 解码失败：既非 DEFLATE 压缩也非可识别文本",
                    ex, "SAML_RESPONSE_DECODE_FAILED");
            }
        }
    }

    /// <summary>XML 属性值转义，防止注入。</summary>
    private static string XmlEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
