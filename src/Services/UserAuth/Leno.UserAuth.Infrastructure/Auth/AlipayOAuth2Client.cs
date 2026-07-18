using System.Globalization;
using System.Text.Json;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.UserAuth.Infrastructure.Auth;

/// <summary>
/// 支付宝开放平台 OAuth2 客户端实现，基于支付宝授权登录流程。
/// 使用支付宝开放平台 OAuth2 端点。
/// </summary>
public sealed class AlipayOAuth2Client : IExternalAuthService
{
    private const string AuthorizationEndpoint = "https://openauth.alipay.com/oauth2/publicAppAuthorize.htm";
    private const string GatewayUrl = "https://openapi.alipay.com/gateway.do";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlipayOAuth2Client> _logger;

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

        // 支付宝 OAuth2 token 交换使用 GET 请求
        var tokenUrl = $"{GatewayUrl}?app_id={Uri.EscapeDataString(appId)}&method=alipay.system.oauth.token&charset=utf-8&sign_type=RSA2&timestamp={Uri.EscapeDataString(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))}&version=1.0&grant_type=authorization_code&code={Uri.EscapeDataString(code)}";

        var response = await _httpClient.GetAsync(tokenUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Alipay token exchange failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("支付宝授权码交换失败", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

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

    public async Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        // 支付宝 GetUserInfoAsync 需要 user_id，但此接口仅传 accessToken
        // 实际使用中应走 ExchangeCodeAsync 流程
        throw new NotSupportedException("支付宝须通过 ExchangeCodeAsync 获取用户信息，请勿直接调用 GetUserInfoAsync(accessToken)");
    }

    private async Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, string? alipayUserId, CancellationToken ct)
    {
        var appId = GetAppId();
        var userInfoUrl = $"{GatewayUrl}?app_id={Uri.EscapeDataString(appId)}&method=alipay.user.info.share&charset=utf-8&sign_type=RSA2&timestamp={Uri.EscapeDataString(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))}&version=1.0&auth_token={Uri.EscapeDataString(accessToken)}";

        var response = await _httpClient.GetAsync(userInfoUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Alipay userinfo failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("获取支付宝用户信息失败", "OAUTH_USERINFO_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

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

        // 支付宝不直接返回邮箱，需用 userId 构造伪邮箱
        var email = $"{userId}@alipay.local";

        return new ExternalLoginInfo(Provider, userId, email, nickName ?? "支付宝用户", avatar);
    }

    private string GetAppId()
    {
        var appId = _configuration["OAuth2:Alipay:AppId"];
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new UserAuthDomainException("支付宝 OAuth2 AppId 未配置", "OAUTH_CONFIG_MISSING");
        }
        return appId;
    }
}