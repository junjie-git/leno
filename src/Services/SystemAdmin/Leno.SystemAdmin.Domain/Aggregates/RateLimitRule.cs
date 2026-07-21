using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 接口限流规则聚合根，管理 API 接口的限流策略。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>RuleId</c>。
/// </summary>
public sealed class RateLimitRule : AggregateRoot
{
    private const int MaxTargetApiLength = 256;
    private const int MaxTargetContextLength = 64;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid RuleId => Id;

    /// <summary>目标 API 路径（如 /api/orders），≤256 字。</summary>
    public string TargetApi { get; private set; } = string.Empty;

    /// <summary>目标上下文（限流维度标识，如 userId、ip），≤64 字，可空。</summary>
    public string? TargetContext { get; private set; }

    /// <summary>限流阈值（时间窗口内最大请求数）。</summary>
    public int Limit { get; private set; }

    /// <summary>时间窗口大小（秒）。</summary>
    public int WindowSeconds { get; private set; }

    /// <summary>限流算法。</summary>
    public LimitAlgorithm Algorithm { get; private set; }

    /// <summary>限流作用域。</summary>
    public LimitScope Scope { get; private set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// 乐观并发控制版本号（RowVersion）。
    /// 由 EF Core 在每次 SaveChanges 时自动递增，配合 <c>IsRowVersion()</c> 配置实现乐观并发控制。
    /// 控制器层捕获 <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> 返回 409 Conflict。
    /// </summary>
    public byte[] Version { get; private set; } = Array.Empty<byte>();

    /// <summary>EF Core 无参构造。</summary>
    private RateLimitRule() { }

    private RateLimitRule(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验各字段合法性，构建限流规则。新规则默认启用。
    /// </summary>
    /// <param name="ruleId">规则标识，由应用层生成。</param>
    /// <param name="targetApi">目标 API 路径。</param>
    /// <param name="targetContext">目标上下文，可空。</param>
    /// <param name="limit">限流阈值。</param>
    /// <param name="windowSeconds">时间窗口大小（秒）。</param>
    /// <param name="algorithm">限流算法。</param>
    /// <param name="scope">限流作用域。</param>
    public static RateLimitRule Create(
        Guid ruleId,
        string targetApi,
        string? targetContext,
        int limit,
        int windowSeconds,
        LimitAlgorithm algorithm,
        LimitScope scope)
    {
        if (ruleId == Guid.Empty)
        {
            throw new SystemAdminDomainException("规则标识不可为空", "RATE_LIMIT_RULE_ID_EMPTY");
        }

        ValidateTargetApi(targetApi);
        ValidateTargetContext(targetContext);
        ValidateLimit(limit);
        ValidateWindowSeconds(windowSeconds);

        return new RateLimitRule(ruleId)
        {
            TargetApi = targetApi.Trim(),
            TargetContext = NormalizeNullable(targetContext),
            Limit = limit,
            WindowSeconds = windowSeconds,
            Algorithm = algorithm,
            Scope = scope,
            Enabled = true
        };
    }

    /// <summary>
    /// 更新限流规则，变更后发布 <see cref="RateLimitRuleUpdatedEvent"/> 供网关热加载。
    /// </summary>
    /// <param name="targetApi">目标 API 路径。</param>
    /// <param name="targetContext">目标上下文，可空。</param>
    /// <param name="limit">限流阈值。</param>
    /// <param name="windowSeconds">时间窗口大小（秒）。</param>
    /// <param name="algorithm">限流算法。</param>
    /// <param name="scope">限流作用域。</param>
    public void Update(
        string targetApi,
        string? targetContext,
        int limit,
        int windowSeconds,
        LimitAlgorithm algorithm,
        LimitScope scope)
    {
        ValidateTargetApi(targetApi);
        ValidateTargetContext(targetContext);
        ValidateLimit(limit);
        ValidateWindowSeconds(windowSeconds);

        TargetApi = targetApi.Trim();
        TargetContext = NormalizeNullable(targetContext);
        Limit = limit;
        WindowSeconds = windowSeconds;
        Algorithm = algorithm;
        Scope = scope;

        AddDomainEvent(new RateLimitRuleUpdatedEvent(Id));
    }

    /// <summary>
    /// 启用限流规则。
    /// </summary>
    public void Enable()
    {
        if (Enabled) return;

        Enabled = true;
        AddDomainEvent(new RateLimitRuleUpdatedEvent(Id));
    }

    /// <summary>
    /// 停用限流规则。
    /// </summary>
    public void Disable()
    {
        if (!Enabled) return;

        Enabled = false;
        AddDomainEvent(new RateLimitRuleUpdatedEvent(Id));
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateTargetApi(string targetApi)
    {
        if (string.IsNullOrWhiteSpace(targetApi))
        {
            throw new SystemAdminDomainException("目标 API 路径不可为空", "RATE_LIMIT_TARGET_API_EMPTY");
        }

        if (targetApi.Trim().Length > MaxTargetApiLength)
        {
            throw new SystemAdminDomainException($"目标 API 路径长度不可超过 {MaxTargetApiLength} 字符", "RATE_LIMIT_TARGET_API_LENGTH");
        }
    }

    private static void ValidateTargetContext(string? targetContext)
    {
        if (!string.IsNullOrWhiteSpace(targetContext) && targetContext.Trim().Length > MaxTargetContextLength)
        {
            throw new SystemAdminDomainException($"目标上下文长度不可超过 {MaxTargetContextLength} 字符", "RATE_LIMIT_TARGET_CONTEXT_LENGTH");
        }
    }

    private static void ValidateLimit(int limit)
    {
        if (limit <= 0)
        {
            throw new SystemAdminDomainException("限流阈值必须大于 0", "RATE_LIMIT_LIMIT_INVALID");
        }
    }

    private static void ValidateWindowSeconds(int windowSeconds)
    {
        if (windowSeconds <= 0)
        {
            throw new SystemAdminDomainException("时间窗口必须大于 0 秒", "RATE_LIMIT_WINDOW_INVALID");
        }
    }
}