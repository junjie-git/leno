using System.Text.Json;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.UserAuth.Infrastructure.Auth;

/// <summary>
/// Google OAuth2 客户端实现，基于 OpenID Connect 流程。
/// 使用 Google Identity Services OAuth2 端点。
/// </summary>
public sealed class GoogleOAuth2Client : IExternalAuthService
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleOAuth2Client> _logger;

    public string Provider => "google";

    public GoogleOAuth2Client(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleOAuth2Client> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClient = httpClientFactory.CreateClient(nameof(GoogleOAuth2Client));
        _configuration = configuration;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string state, string redirectUri)
    {
        var clientId = GetClientId();
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        };

        var queryString = string.Join("&", query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return $"{AuthorizationEndpoint}?{queryString}";
    }

    public async Task<ExternalLoginInfo> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var clientId = GetClientId();
        var clientSecret = GetClientSecret();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync(TokenEndpoint, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google token exchange failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("Google 授权码交换失败", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var tokenDoc = JsonDocument.Parse(body);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()
            ?? throw new UserAuthDomainException("Google 未返回访问令牌", "OAUTH_TOKEN_EMPTY");

        return await GetUserInfoAsync(accessToken, ct);
    }

    public async Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google userinfo failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("获取 Google 用户信息失败", "OAUTH_USERINFO_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var sub = root.GetProperty("sub").GetString()
            ?? throw new UserAuthDomainException("Google 未返回用户标识", "OAUTH_USER_ID_EMPTY");

        var email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var picture = root.TryGetProperty("picture", out var picEl) ? picEl.GetString() : null;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UserAuthDomainException("Google 未返回邮箱", "OAUTH_EMAIL_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = email!.Split('@')[0];
        }

        return new ExternalLoginInfo(Provider, sub, email!, name, picture);
    }

    private string GetClientId()
    {
        var clientId = _configuration["OAuth2:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new UserAuthDomainException("Google OAuth2 ClientId 未配置", "OAUTH_CONFIG_MISSING");
        }
        return clientId;
    }

    private string GetClientSecret()
    {
        var clientSecret = _configuration["OAuth2:Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new UserAuthDomainException("Google OAuth2 ClientSecret 未配置", "OAUTH_CONFIG_MISSING");
        }
        return clientSecret;
    }
}