using Leno.Infrastructure.AntiCorruption;
using Leno.Infrastructure.Auth;
using Leno.Notification.Domain.Services;
using Leno.SharedContracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 用户联系方式防腐层，通过 HTTP 调用用户域内部端点获取手机号与邮箱。
/// 继承 <see cref="AntiCorruptionBase"/>，远程失败统一抛 <see cref="AntiCorruptionException"/>，不再返回 null。
/// </summary>
public sealed class UserContactAntiCorruptionService : AntiCorruptionBase, IUserContactService
{
    private readonly HttpClient _httpClient;
    private readonly InternalApiKeyOptions _internalKeyOptions;
    private readonly ILogger<UserContactAntiCorruptionService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override string ServiceName => "user_contact";

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
    public Task<UserContactInfo?> GetContactsAsync(Guid userId, CancellationToken ct = default)
        => ExecuteAsync("get_contacts", async token =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/users/{userId}/contacts");
            request.Headers.Add("X-Internal-Key", _internalKeyOptions.ApiKey);

            using var response = await _httpClient.SendAsync(request, token);
            EnsureSuccessStatusCode(response, "get_contacts");

            var json = await response.Content.ReadAsStringAsync(token);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<UserContactsDto>>(json, JsonOptions);
            if (apiResponse is null || apiResponse.Code != 200 || apiResponse.Data is null)
            {
                throw new AntiCorruptionException(
                    $"用户域返回空联系方式（userId={userId}）",
                    "USER_CONTACT_REMOTE_FAILED");
            }

            return (UserContactInfo?)new UserContactInfo
            {
                UserId = apiResponse.Data.UserId,
                Email = apiResponse.Data.Email,
                PhoneNumber = apiResponse.Data.PhoneNumber
            };
        }, ct);
}

/// <summary>用户联系方式（未脱敏），与用户域内部端点返回结构对应。</summary>
public sealed class UserContactsDto
{
    public Guid UserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
