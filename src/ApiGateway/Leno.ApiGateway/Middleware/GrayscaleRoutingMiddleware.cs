using Leno.ApiGateway.Options;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// 域拆分迁移阶段2：灰度路由决策中间件。
/// <para>
/// 在 YARP 路由匹配之前执行，根据用户 ID hash 计算灰度决策，并在请求头中注入
/// <c>X-Grayscale-Decision</c>（值为 <c>new</c> 或 <c>old</c>）。
/// YARP 路由配置通过 Header 匹配器（<c>Match.Headers</c>）选择新域或旧域路由：
/// <list type="bullet">
///   <item>新域路由（Order=5）：匹配 <c>X-Grayscale-Decision: new</c></item>
///   <item>旧域路由（Order=10）：无 Header 匹配，作为兜底</item>
/// </list>
/// </para>
/// <para>
/// 灰度策略（参考 Spec §3.3.2）：
/// <list type="bullet">
///   <item>灰度维度：按用户 ID hash（<c>hash(userId) % 100 &lt; Threshold</c>）</item>
///   <item>灰度梯度：5% → 25% → 50% → 100%</item>
///   <item>内部端点：<c>internal/v1/*</c> 100% 切新域（<see cref="GrayscaleOptions.InternalSwitchAllToNew"/></item>
///   <item>回滚机制：<see cref="GrayscaleOptions.RollbackToLegacy"/> 一键切回旧域</item>
/// </list>
/// </para>
/// <para>
/// 安全性：中间件会先移除客户端传入的 <c>X-Grayscale-Decision</c> 头，防止客户端伪造灰度决策。
/// 决策头由网关内部生成，<see cref="Transforms.UserContextTransformProvider"/> 会在转发到后端前移除该头。
/// </para>
/// <para>
/// 使用 <see cref="IOptionsMonitor{TOptions}"/> 绑定以支持运行时热更新（appsettings.json 或 Consul KV），
/// 回滚 TTL &lt; 30 秒。
/// </para>
/// </summary>
public sealed class GrayscaleRoutingMiddleware
{
    /// <summary>
    /// 灰度决策请求头名称。YARP 路由通过此头匹配新域/旧域路由。
    /// 中间件设置此头，<see cref="Transforms.UserContextTransformProvider"/> 在转发前移除。
    /// </summary>
    public const string DecisionHeader = "X-Grayscale-Decision";

    /// <summary>决策值：走新域。</summary>
    public const string DecisionNew = "new";

    /// <summary>决策值：走旧域。</summary>
    public const string DecisionOld = "old";

    /// <summary>
    /// JWT Sub Claim 名称，与 <see cref="Transforms.UserContextTransformProvider"/> 对齐。
    /// </summary>
    private const string ClaimSub = "Sub";

    /// <summary>
    /// 测试角色头：用于测试场景模拟特定用户 hash，当 JWT 缺失时作为 userId 输入。
    /// 生产环境应由 <see cref="Transforms.UserContextTransformProvider"/> 在转发前移除。
    /// </summary>
    private const string TestRoleHeader = "X-Test-Role";

    /// <summary>
    /// 内部端点路径前缀。匹配此前缀的路径在 <see cref="GrayscaleOptions.InternalSwitchAllToNew"/> 为 true 时 100% 切新域。
    /// </summary>
    private const string InternalPathPrefix = "/internal/v1/";

    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<GrayscaleOptions> _options;
    private readonly ILogger<GrayscaleRoutingMiddleware> _logger;

    public GrayscaleRoutingMiddleware(
        RequestDelegate next,
        IOptionsMonitor<GrayscaleOptions> options,
        ILogger<GrayscaleRoutingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 计算灰度决策并注入请求头，然后放行到下游中间件（YARP 路由匹配）。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = _options.CurrentValue;
        var path = context.Request.Path.Value ?? string.Empty;

        // 安全：移除客户端可能伪造的决策头，防止绕过灰度判定
        context.Request.Headers.Remove(DecisionHeader);

        var decision = ComputeDecision(context, path, options);
        context.Request.Headers[DecisionHeader] = decision;

        // 调试日志：记录灰度决策（Debug 级别避免生产环境性能影响）
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var userId = ResolveUserId(context);
            _logger.LogDebug(
                "Grayscale decision: path={Path}, userId={UserId}, threshold={Threshold}, decision={Decision}",
                path, userId, options.Threshold, decision);
        }

        await _next(context);
    }

    /// <summary>
    /// 计算灰度决策（new/old），遵循以下优先级：
    /// <list type="number">
    ///   <item>回滚开关 <see cref="GrayscaleOptions.RollbackToLegacy"/> = true → old</item>
    ///   <item>灰度未启用 <see cref="GrayscaleOptions.Enabled"/> = false → old</item>
    ///   <item>内部端点 + <see cref="GrayscaleOptions.InternalSwitchAllToNew"/> = true → new</item>
    ///   <item>按 userId hash 与 <see cref="GrayscaleOptions.Threshold"/> 比较 → new/old</item>
    /// </list>
    /// </summary>
    /// <param name="context">HTTP 上下文（用于读取 JWT Claims 和请求头）。</param>
    /// <param name="path">请求路径（已提取避免重复读取）。</param>
    /// <param name="options">当前灰度配置（已热更新的快照）。</param>
    /// <returns><see cref="DecisionNew"/> 或 <see cref="DecisionOld"/>。</returns>
    internal static string ComputeDecision(HttpContext context, string path, GrayscaleOptions options)
    {
        // 1. 回滚优先：一键切回旧域
        if (options.RollbackToLegacy)
        {
            return DecisionOld;
        }

        // 2. 灰度未启用：所有请求走旧域
        if (!options.Enabled)
        {
            return DecisionOld;
        }

        // 3. 内部端点：100% 切新域（Spec §3.3.2）
        if (options.InternalSwitchAllToNew
            && path.StartsWith(InternalPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return DecisionNew;
        }

        // 4. 按 userId hash 分流
        var userId = ResolveUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            // 未认证请求（如 login/register/refresh-token）走旧域
            // 这些端点在白名单中，不需要灰度判定
            return DecisionOld;
        }

        var hashBucket = ComputeDeterministicHash(userId);
        return hashBucket < options.Threshold ? DecisionNew : DecisionOld;
    }

    /// <summary>
    /// 解析用户 ID，优先级：
    /// <list type="number">
    ///   <item>JWT <c>Sub</c> Claim（生产环境，由 <c>UseAuthentication</c> 填充）</item>
    ///   <item><c>X-Test-Role</c> 请求头（测试环境，用于模拟特定用户 hash 桶）</item>
    /// </list>
    /// 返回 null/空字符串表示未认证请求。
    /// </summary>
    internal static string ResolveUserId(HttpContext context)
    {
        // 优先从 JWT Sub claim 读取（生产环境）
        var userId = context.User?.FindFirst(ClaimSub)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return userId;
        }

        // 测试角色头（测试环境模拟特定用户）
        var testRole = context.Request.Headers[TestRoleHeader].ToString();
        if (!string.IsNullOrEmpty(testRole))
        {
            return testRole;
        }

        return string.Empty;
    }

    /// <summary>
    /// 计算确定性 hash（FNV-1a 32-bit），返回 0-99 的桶值。
    /// <para>
    /// FNV-1a 算法特性：
    /// <list type="bullet">
    ///   <item>确定性：相同输入始终产生相同输出（跨进程、跨机器一致）</item>
    ///   <item>均匀分布：hash 值在 0-99 区间近似均匀分布</item>
    ///   <item>高性能：仅涉及 XOR 和乘法，无加密开销</item>
    /// </list>
    /// </para>
    /// <para>
    /// 注意：<see cref="string.GetHashCode()"/> 在 .NET 中非跨进程一致（不同进程可能不同），
    /// 因此不能用于灰度判定。FNV-1a 是非加密但确定性的 hash，适合此场景。
    /// </para>
    /// </summary>
    /// <param name="value">待 hash 的字符串（userId 或 testRole）。</param>
    /// <returns>0-99 的桶值。</returns>
    internal static int ComputeDeterministicHash(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        // FNV-1a 32-bit hash
        uint hash = 2166136261; // FNV offset basis
        foreach (var c in value)
        {
            hash ^= c;
            hash *= 16777619; // FNV prime
        }

        return (int)(hash % 100);
    }
}
