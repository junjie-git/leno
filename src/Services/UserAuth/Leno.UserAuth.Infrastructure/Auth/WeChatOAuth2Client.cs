using System.Text.Json;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.UserAuth.Infrastructure.Auth;

/// <summary>
/// 微信开放平台 OAuth2 客户端实现，基于微信扫码登录流程。
/// 使用微信开放平台 OAuth2 端点。
/// </summary>
public sealed class WeChatOAuth2Client : IExternalAuthService
{
    private const string AuthorizationEndpoint = "https://open.weixin.qq.com/connect/qrconnect";
    private const string TokenEndpoint = "https://api.weixin.qq.com/sns/oauth2/access_token";
    private const string UserInfoEndpoint = "https://api.weixin.qq.com/sns/userinfo";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeChatOAuth2Client> _logger;

    public string Provider => "wechat";

    public WeChatOAuth2Client(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WeChatOAuth2Client> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClient = httpClientFactory.CreateClient(nameof(WeChatOAuth2Client));
        _configuration = configuration;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string state, string redirectUri)
    {
        var appId = GetAppId();
        var query = new Dictionary<string, string?>
        {
            ["appid"] = appId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "snsapi_login",
            ["state"] = state
        };

        var queryString = string.Join("&", query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        // 微信要求末尾追加 #wechat_redirect
        return $"{AuthorizationEndpoint}?{queryString}#wechat_redirect";
    }

    public async Task<ExternalLoginInfo> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var appId = GetAppId();
        var appSecret = GetAppSecret();

        var tokenUrl = $"{TokenEndpoint}?appid={Uri.EscapeDataString(appId)}&secret={Uri.EscapeDataString(appSecret)}&code={Uri.EscapeDataString(code)}&grant_type=authorization_code";

        var response = await _httpClient.GetAsync(tokenUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("WeChat token exchange failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("微信授权码交换失败", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var doc = JsonDocument.Parse(body);

        // 微信错误响应
        if (doc.RootElement.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() != 0)
        {
            var errMsg = doc.RootElement.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
            _logger.LogError("WeChat token exchange error: {ErrCode} {ErrMsg}", errCode.GetInt32(), errMsg);
            throw new UserAuthDomainException($"微信授权失败: {errMsg}", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new UserAuthDomainException("微信未返回访问令牌", "OAUTH_TOKEN_EMPTY");

        var openId = doc.RootElement.GetProperty("openid").GetString()
            ?? throw new UserAuthDomainException("微信未返回用户标识", "OAUTH_USER_ID_EMPTY");

        return await GetUserInfoAsync(accessToken, openId, ct);
    }

    public async Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        // 微信 GetUserInfoAsync 需要 openId，但此接口仅传 accessToken 无法获取 openId
        // 实际使用中应走 ExchangeCodeAsync 流程
        throw new NotSupportedException("微信须通过 ExchangeCodeAsync 获取用户信息，请勿直接调用 GetUserInfoAsync(accessToken)");
    }

    private async Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, string openId, CancellationToken ct)
    {
        var userInfoUrl = $"{UserInfoEndpoint}?access_token={Uri.EscapeDataString(accessToken)}&openid={Uri.EscapeDataString(openId)}";

        var response = await _httpClient.GetAsync(userInfoUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("WeChat userinfo failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("获取微信用户信息失败", "OAUTH_USERINFO_FAILED");
        }

        var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() != 0)
        {
            var errMsg = doc.RootElement.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
            _logger.LogError("WeChat userinfo error: {ErrCode} {ErrMsg}", errCode.GetInt32(), errMsg);
            throw new UserAuthDomainException($"获取微信用户信息失败: {errMsg}", "OAUTH_USERINFO_FAILED");
        }

        var unionId = doc.RootElement.TryGetProperty("unionid", out var uId) ? uId.GetString() : openId;
        var nickname = doc.RootElement.TryGetProperty("nickname", out var nick) ? nick.GetString() : null;
        var headImgUrl = doc.RootElement.TryGetProperty("headimgurl", out var img) ? img.GetString() : null;

        // 微信不返回邮箱，Email 传 null，避免伪邮箱入库污染下游集成事件
        return new ExternalLoginInfo(Provider, unionId ?? openId, null, nickname ?? "微信用户", headImgUrl);
    }

    private string GetAppId()
    {
        var appId = _configuration["OAuth2:WeChat:AppId"];
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new UserAuthDomainException("微信 OAuth2 AppId 未配置", "OAUTH_CONFIG_MISSING");
        }
        return appId;
    }

    private string GetAppSecret()
    {
        var appSecret = _configuration["OAuth2:WeChat:AppSecret"];
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            throw new UserAuthDomainException("微信 OAuth2 AppSecret 未配置", "OAUTH_CONFIG_MISSING");
        }
        return appSecret;
    }
}