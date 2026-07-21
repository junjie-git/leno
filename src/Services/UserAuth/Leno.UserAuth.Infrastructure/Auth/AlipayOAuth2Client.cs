using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.UserAuth.Infrastructure.Auth;

/// <summary>
/// 支付宝开放平台 OAuth2 客户端实现，基于支付宝授权登录流程。
/// 所有 API 请求按支付宝规范做 RSA2 签名，响应用支付宝公钥做验签。
/// </summary>
public sealed class AlipayOAuth2Client : IExternalAuthService
{
    private const string AuthorizationEndpoint = "https://openauth.alipay.com/oauth2/publicAppAuthorize.htm";
    private const string GatewayUrl = "https://openapi.alipay.com/gateway.do";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlipayOAuth2Client> _logger;
    private readonly RSA _merchantPrivateKey;
    private readonly RSA? _alipayPublicKey;

    public string Provider => "alipay";

    public AlipayOAuth2Client(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AlipayOAuth2Client> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClient = httpClientFactory.CreateClient(nameof(AlipayOAuth2Client));
        _configuration = configuration;
        _logger = logger;

        _merchantPrivateKey = LoadRsaPrivateKey(GetRequiredConfig("OAuth2:Alipay:MerchantPrivateKey"));
        var alipayPublicKeyPem = _configuration["OAuth2:Alipay:AlipayPublicKey"];
        if (!string.IsNullOrWhiteSpace(alipayPublicKeyPem))
        {
            _alipayPublicKey = LoadRsaPublicKey(alipayPublicKeyPem);
        }
    }

    public string GetAuthorizationUrl(string state, string redirectUri)
    {
        var appId = GetAppId();
        var query = new Dictionary<string, string?>
        {
            ["app_id"] = appId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "auth_user",
            ["state"] = state
        };

        var queryString = string.Join("&", query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return $"{AuthorizationEndpoint}?{queryString}";
    }

    public async Task<ExternalLoginInfo> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var appId = GetAppId();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var businessParams = new Dictionary<string, string?>
        {
            ["app_id"] = appId,
            ["method"] = "alipay.system.oauth.token",
            ["charset"] = "utf-8",
            ["sign_type"] = "RSA2",
            ["timestamp"] = timestamp,
            ["version"] = "1.0",
            ["grant_type"] = "authorization_code",
            ["code"] = code
        };

        var signed = BuildSignedParameters(businessParams);
        var tokenUrl = BuildGatewayUrl(signed);
        var response = await _httpClient.GetAsync(tokenUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Alipay token exchange failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("支付宝授权码交换失败", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // 整体验签（支付宝响应 sign 字段在根层）
        if (root.TryGetProperty("sign", out var signEl) && _alipayPublicKey is not null)
        {
            var sign = signEl.GetString() ?? string.Empty;
            // 提取响应数据：以 alipay_xxx_response 节点为准
            var responseNode = root.EnumerateObject().FirstOrDefault(p => p.Name.EndsWith("_response", StringComparison.OrdinalIgnoreCase));
            if (responseNode.Value.ValueKind == JsonValueKind.Object)
            {
                var responseJson = responseNode.Value.GetRawText();
                if (!VerifyResponseSignRaw(responseJson, sign))
                {
                    _logger.LogError("Alipay token exchange response sign verification failed");
                    throw new UserAuthDomainException("支付宝响应验签失败", "OAUTH_RESPONSE_SIGN_INVALID");
                }
            }
        }

        var responseData = root.TryGetProperty("alipay_system_oauth_token_response", out var tokenResp)
            ? tokenResp
            : root;

        if (responseData.TryGetProperty("code", out var codeEl) && codeEl.GetString() != "10000")
        {
            var msg = responseData.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : "未知错误";
            _logger.LogError("Alipay token exchange error: {Code} {Msg}", codeEl.GetString(), msg);
            throw new UserAuthDomainException($"支付宝授权失败: {msg}", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var accessToken = responseData.GetProperty("access_token").GetString()
            ?? throw new UserAuthDomainException("支付宝未返回访问令牌", "OAUTH_TOKEN_EMPTY");

        var alipayUserId = responseData.TryGetProperty("user_id", out var userIdEl) ? userIdEl.GetString() : null;

        return await GetUserInfoAsync(accessToken, alipayUserId, ct);
    }

    public Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        throw new NotSupportedException("支付宝须通过 ExchangeCodeAsync 获取用户信息，请勿直接调用 GetUserInfoAsync(accessToken)");
    }

    private async Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, string? alipayUserId, CancellationToken ct)
    {
        var appId = GetAppId();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var businessParams = new Dictionary<string, string?>
        {
            ["app_id"] = appId,
            ["method"] = "alipay.user.info.share",
            ["charset"] = "utf-8",
            ["sign_type"] = "RSA2",
            ["timestamp"] = timestamp,
            ["version"] = "1.0",
            ["auth_token"] = accessToken
        };

        var signed = BuildSignedParameters(businessParams);
        var userInfoUrl = BuildGatewayUrl(signed);

        var response = await _httpClient.GetAsync(userInfoUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Alipay userinfo failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("获取支付宝用户信息失败", "OAUTH_USERINFO_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("sign", out var signEl) && _alipayPublicKey is not null)
        {
            var sign = signEl.GetString() ?? string.Empty;
            var responseNode = root.EnumerateObject().FirstOrDefault(p => p.Name.EndsWith("_response", StringComparison.OrdinalIgnoreCase));
            if (responseNode.Value.ValueKind == JsonValueKind.Object)
            {
                var responseJson = responseNode.Value.GetRawText();
                if (!VerifyResponseSignRaw(responseJson, sign))
                {
                    _logger.LogError("Alipay userinfo response sign verification failed");
                    throw new UserAuthDomainException("支付宝响应验签失败", "OAUTH_RESPONSE_SIGN_INVALID");
                }
            }
        }

        var responseData = root.TryGetProperty("alipay_user_info_share_response", out var infoResp)
            ? infoResp
            : root;

        if (responseData.TryGetProperty("code", out var codeEl) && codeEl.GetString() != "10000")
        {
            var msg = responseData.TryGetProperty("sub_msg", out var msgEl) ? msgEl.GetString() : "未知错误";
            _logger.LogError("Alipay userinfo error: {Code} {Msg}", codeEl.GetString(), msg);
            throw new UserAuthDomainException($"获取支付宝用户信息失败: {msg}", "OAUTH_USERINFO_FAILED");
        }

        var userId = alipayUserId ?? (responseData.TryGetProperty("user_id", out var uid) ? uid.GetString() : null);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UserAuthDomainException("支付宝未返回用户标识", "OAUTH_USER_ID_EMPTY");
        }

        var avatar = responseData.TryGetProperty("avatar", out var av) ? av.GetString() : null;
        var nickName = responseData.TryGetProperty("nick_name", out var nn) ? nn.GetString() : null;
        var email = $"{userId}@alipay.local";

        return new ExternalLoginInfo(Provider, userId, email, nickName ?? "支付宝用户", avatar);
    }

    /// <summary>
    /// 对业务参数做 RSA2 签名并返回包含 sign_type / sign 的完整参数集合。
    /// </summary>
    internal Dictionary<string, string?> BuildSignedParameters(IReadOnlyDictionary<string, string?> businessParams)
    {
        var withSignType = new Dictionary<string, string?>(businessParams)
        {
            ["sign_type"] = "RSA2"
        };

        var sign = ComputeSign(withSignType);
        withSignType["sign"] = sign;
        return withSignType;
    }

    /// <summary>
    /// 按 ASCII 字典序拼接所有非空业务参数（不含 sign / sign_type），用商户私钥做 RSA-SHA256 签名，Base64 编码返回。
    /// </summary>
    internal string ComputeSign(IReadOnlyDictionary<string, string?> parameters)
    {
        var sortedPairs = parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value) && kv.Key != "sign" && kv.Key != "sign_type")
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}");

        var content = string.Join("&", sortedPairs);
        var dataBytes = Encoding.UTF8.GetBytes(content);
        var signature = _merchantPrivateKey.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// 验证支付宝响应签名：对响应数据 JSON 重新签名并比较。
    /// </summary>
    internal bool VerifyResponseSign(IReadOnlyDictionary<string, string?> responseData, string sign)
    {
        if (_alipayPublicKey is null)
        {
            _logger.LogWarning("AlipayPublicKey 未配置，跳过响应验签");
            return true;
        }

        var sortedPairs = responseData
            .Where(kv => !string.IsNullOrEmpty(kv.Value) && kv.Key != "sign" && kv.Key != "sign_type")
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}");

        var content = string.Join("&", sortedPairs);
        var dataBytes = Encoding.UTF8.GetBytes(content);
        var signBytes = Convert.FromBase64String(sign);

        return _alipayPublicKey.VerifyData(dataBytes, signBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// 验证支付宝响应签名（原始 JSON 字节序）：直接对响应节点 JSON 文本验签。
    /// </summary>
    private bool VerifyResponseSignRaw(string responseJson, string sign)
    {
        if (_alipayPublicKey is null)
        {
            return true;
        }

        var dataBytes = Encoding.UTF8.GetBytes(responseJson);
        var signBytes = Convert.FromBase64String(sign);
        return _alipayPublicKey.VerifyData(dataBytes, signBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string BuildGatewayUrl(IReadOnlyDictionary<string, string?> parameters)
    {
        var query = string.Join("&", parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return $"{GatewayUrl}?{query}";
    }

    private static RSA LoadRsaPrivateKey(string pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }

    private static RSA LoadRsaPublicKey(string pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }

    private string GetAppId()
    {
        return GetRequiredConfig("OAuth2:Alipay:AppId");
    }

    private string GetRequiredConfig(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserAuthDomainException($"支付宝 OAuth2 配置缺失：{key}", "OAUTH_CONFIG_MISSING");
        }
        return value;
    }
}
