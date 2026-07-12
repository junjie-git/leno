using Leno.Infrastructure.Auth;
using Leno.Notification.Domain.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 用户联系方式防腐层，通过 HTTP 调用用户域内部端点获取手机号与邮箱。
/// </summary>
public sealed class UserContactAntiCorruptionService : IUserContactService
{
    private readonly HttpClient _httpClient;
    private readonly InternalApiKeyOptions _internalKeyOptions;
    private readonly ILogger<UserContactAntiCorruptionService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public UserContactAntiCorruptionService(
        HttpClient httpClient,
        IOptions<InternalApiKeyOptions> internalKeyOptions,
        ILogger<UserContactAntiCorruptionService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(internalKeyOptions);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _internalKeyOptions = internalKeyOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserContactInfo?> GetContactsAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/users/{userId}/contacts");
            request.Headers.Add("X-Internal-Key", _internalKeyOptions.ApiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("查询用户联系方式失败 UserId={UserId} Status={Status}", userId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserContactsDto>>(json, JsonOptions);
            if (apiResponse is null || apiResponse.Code != 200 || apiResponse.Data is null)
            {
                _logger.LogWarning("用户联系方式响应为空或失败 UserId={UserId}", userId);
                return null;
            }

            return new UserContactInfo
            {
                UserId = apiResponse.Data.UserId,
                Email = apiResponse.Data.Email,
                PhoneNumber = apiResponse.Data.PhoneNumber
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询用户联系方式异常 UserId={UserId}", userId);
            return null;
        }
    }
}

/// <summary>用户联系方式（未脱敏），与用户域内部端点返回结构对应。</summary>
public sealed class UserContactsDto
{
    public Guid UserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}