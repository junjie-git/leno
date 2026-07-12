using System.Diagnostics;
using System.Text.Json;
using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Leno.UserAuth.Infrastructure.Audit;

/// <summary>
/// 审计日志中间件，拦截对 /api/admin/ 路径的 POST/PUT/DELETE 操作，
/// 自动创建审计日志记录。审计日志写入与业务操作在同一事务中（通过共享 DbContext）。
/// 审计日志仅追加，不可修改不可删除。
/// </summary>
public sealed class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;
    private static readonly HashSet<string> AuditableMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var shouldAudit = ShouldAudit(context);

        if (!shouldAudit)
        {
            await _next(context);
            return;
        }

        // 将审计上下文存入 HttpContext.Items，供后续 AuditLog 实体使用
        context.Items["AuditLog:Action"] = ResolveAction(context);
        context.Items["AuditLog:ResourceType"] = ResolveResourceType(context);
        context.Items["AuditLog:ResourceId"] = ResolveResourceId(context);
        context.Items["AuditLog:OperatorId"] = ResolveOperatorId(context);

        await _next(context);
    }

    /// <summary>
    /// 判断当前请求是否需要审计：仅拦截 /api/admin/ 路径的 POST/PUT/DELETE 操作。
    /// </summary>
    private bool ShouldAudit(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return AuditableMethods.Contains(context.Request.Method);
    }

    private static string ResolveAction(HttpContext context)
    {
        var method = context.Request.Method.ToUpperInvariant();
        var path = context.Request.Path.Value ?? string.Empty;

        return method switch
        {
            "POST" => path.Contains("/enable", StringComparison.OrdinalIgnoreCase) ? "Enable"
                : path.Contains("/disable", StringComparison.OrdinalIgnoreCase) ? "Disable"
                : "Create",
            "PUT" => "Update",
            "PATCH" => "Patch",
            "DELETE" => "Delete",
            _ => method
        };
    }

    private static string ResolveResourceType(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.Contains("/oauth-clients", StringComparison.OrdinalIgnoreCase))
            return "OAuthClient";
        if (path.Contains("/roles", StringComparison.OrdinalIgnoreCase))
            return "Role";
        if (path.Contains("/users", StringComparison.OrdinalIgnoreCase))
            return "User";
        if (path.Contains("/permissions", StringComparison.OrdinalIgnoreCase))
            return "Permission";

        return "Unknown";
    }

    private static string? ResolveResourceId(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // 尝试从路径中提取 GUID 资源标识
        foreach (var segment in segments)
        {
            if (Guid.TryParse(segment, out _))
            {
                return segment;
            }
        }

        return null;
    }

    private static string? ResolveOperatorId(HttpContext context)
    {
        // 从 JWT Claims 中提取用户标识
        var subClaim = context.User?.FindFirst("sub")?.Value
            ?? context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        return subClaim;
    }
}