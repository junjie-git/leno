using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 告警静默规则聚合根，对应 Alertmanager silence。
/// 在 StartsAt ~ EndsAt 时间窗口内，匹配 Matchers 的告警不再通知。
/// 该聚合为只读消费视图，由 <see cref="Services.IAlertmanagerClient"/> 拉取后构建，不直接落库。
/// </summary>
public sealed class AlertSilence : AggregateRoot
{
    private const int MaxReasonLength = 1000;
    private const int MaxCreatedByLength = 64;
    private const int MaxDurationLength = 64;
    private const int MaxMatchersCount = 32;

    // 匹配器 JSON 与 Alertmanager API 及 AlertSilenceAppService.CreateAsync 输出一致（camelCase），
    // 反序列化须启用大小写不敏感以兼容 PascalCase / camelCase 两种来源。
    private static readonly System.Text.Json.JsonSerializerOptions MatcherJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>匹配器集合（JSON 持久化），命中即静默。</summary>
    public string Matchers { get; private set; } = "[]";

    /// <summary>持续时长描述，如 "2h"、"1d"，由前端传入并原样存储。</summary>
    public string Duration { get; private set; } = string.Empty;

    /// <summary>静默原因。</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>静默起始时间（UTC）。</summary>
    public DateTime StartsAt { get; private set; }

    /// <summary>静默结束时间（UTC）。</summary>
    public DateTime EndsAt { get; private set; }

    /// <summary>创建人标识。</summary>
    public new string CreatedBy { get; private set; } = string.Empty;

    /// <summary>创建时间（UTC）。</summary>
    public new DateTime CreatedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private AlertSilence() { }

    private AlertSilence(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验字段并构建静默规则聚合。
    /// </summary>
    /// <param name="id">静默规则标识。</param>
    /// <param name="matchersJson">匹配器 JSON 数组，如 [{"name":"module","value":"Payment","isRegex":false}]。</param>
    /// <param name="duration">持续时长描述，如 "2h"。</param>
    /// <param name="reason">静默原因。</param>
    /// <param name="startsAt">起始时间（UTC）。</param>
    /// <param name="endsAt">结束时间（UTC）。</param>
    /// <param name="createdBy">创建人标识。</param>
    /// <param name="createdAt">创建时间（UTC）。</param>
    public static AlertSilence Create(
        Guid id,
        string matchersJson,
        string duration,
        string reason,
        DateTime startsAt,
        DateTime endsAt,
        string createdBy,
        DateTime createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new SystemAdminDomainException("静默规则标识不可为空", "ALERT_SILENCE_ID_EMPTY");
        }
        ValidateMatchersJson(matchersJson);
        ValidateDuration(duration);
        ValidateReason(reason);
        ValidateCreatedBy(createdBy);
        ValidateTimeRange(startsAt, endsAt);

        return new AlertSilence(id)
        {
            Matchers = matchersJson.Trim(),
            Duration = duration.Trim(),
            Reason = reason.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedBy = createdBy.Trim(),
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// 解析匹配器 JSON 为 <see cref="AlertMatcher"/> 集合。
    /// 解析失败时返回空集合，由调用方决定如何处理（默认不阻塞静默规则删除）。
    /// </summary>
    public List<AlertMatcher> GetMatchers()
    {
        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<MatcherPayload>>(Matchers, MatcherJsonOptions);
            if (list is null || list.Count == 0)
            {
                return new List<AlertMatcher>();
            }
            if (list.Count > MaxMatchersCount)
            {
                throw new SystemAdminDomainException($"匹配器数量不可超过 {MaxMatchersCount}", "ALERT_SILENCE_MATCHERS_TOO_MANY");
            }
            return list
                .Select(m => new AlertMatcher(m.Name ?? string.Empty, m.Value ?? string.Empty, m.IsRegex))
                .ToList();
        }
        catch (SystemAdminDomainException)
        {
            throw;
        }
        catch
        {
            return new List<AlertMatcher>();
        }
    }

    /// <summary>判断当前时间是否在静默窗口内。</summary>
    public bool IsExpired(DateTime? atUtc = null)
    {
        var now = atUtc ?? DateTime.UtcNow;
        return now >= EndsAt;
    }

    private static void ValidateMatchersJson(string matchersJson)
    {
        if (string.IsNullOrWhiteSpace(matchersJson))
        {
            throw new SystemAdminDomainException("匹配器不可为空", "ALERT_SILENCE_MATCHERS_EMPTY");
        }
        var trimmed = matchersJson.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
        {
            throw new SystemAdminDomainException("匹配器必须为 JSON 数组", "ALERT_SILENCE_MATCHERS_INVALID_JSON");
        }
    }

    private static void ValidateDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            throw new SystemAdminDomainException("持续时长不可为空", "ALERT_SILENCE_DURATION_EMPTY");
        }
        if (duration.Trim().Length > MaxDurationLength)
        {
            throw new SystemAdminDomainException($"持续时长描述长度不可超过 {MaxDurationLength} 字符", "ALERT_SILENCE_DURATION_LENGTH");
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new SystemAdminDomainException("静默原因不可为空", "ALERT_SILENCE_REASON_EMPTY");
        }
        if (reason.Trim().Length > MaxReasonLength)
        {
            throw new SystemAdminDomainException($"静默原因长度不可超过 {MaxReasonLength} 字符", "ALERT_SILENCE_REASON_LENGTH");
        }
    }

    private static void ValidateCreatedBy(string createdBy)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new SystemAdminDomainException("创建人标识不可为空", "ALERT_SILENCE_CREATED_BY_EMPTY");
        }
        if (createdBy.Trim().Length > MaxCreatedByLength)
        {
            throw new SystemAdminDomainException($"创建人标识长度不可超过 {MaxCreatedByLength} 字符", "ALERT_SILENCE_CREATED_BY_LENGTH");
        }
    }

    private static void ValidateTimeRange(DateTime startsAt, DateTime endsAt)
    {
        if (endsAt <= startsAt)
        {
            throw new SystemAdminDomainException("结束时间必须晚于起始时间", "ALERT_SILENCE_TIME_RANGE_INVALID");
        }
    }

    private sealed class MatcherPayload
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public bool IsRegex { get; set; }
    }
}
