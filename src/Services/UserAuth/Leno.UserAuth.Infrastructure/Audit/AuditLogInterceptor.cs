using System.Diagnostics;
using Leno.UserAuth.Domain.Aggregates;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Leno.UserAuth.Infrastructure.Audit;

/// <summary>
/// 审计日志拦截器，在保存变更前从当前 HTTP 请求上下文填充 IP、User-Agent、TraceId 等技术上下文。
/// 应用服务创建 <see cref="AuditLog"/> 时仅写入业务字段，技术上下文由此拦截器统一通过
/// <see cref="AuditLog.Enrich(string?, string?, string?)"/> 聚合方法注入，避免直接操作 EF 元数据 API。
/// </summary>
public sealed class AuditLogInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnrichAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnrichAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnrichAuditLogs(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var ip = ResolveIp(httpContext);
        var userAgent = ResolveUserAgent(httpContext);
        var traceId = ResolveTraceId(httpContext);

        foreach (var entry in context.ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            // 调用聚合根的 Enrich 行为方法，由聚合自身决定是否写入字段（仅当字段为空时填充），
            // 不再直接操作 EF Property().CurrentValue，保持聚合封装完整性。
            entry.Entity.Enrich(ip, userAgent, traceId);
        }
    }

    private static string? ResolveIp(HttpContext? context)
    {
        if (context is null)
        {
            return null;
        }

        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var ip = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? ResolveUserAgent(HttpContext? context)
    {
        if (context is null)
        {
            return null;
        }

        var ua = context.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua;
    }

    private static string? ResolveTraceId(HttpContext? context)
    {
        if (context is null)
        {
            return null;
        }

        return Activity.Current?.Id ?? context.TraceIdentifier;
    }
}
