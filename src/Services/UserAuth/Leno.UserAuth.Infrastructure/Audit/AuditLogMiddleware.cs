using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Leno.UserAuth.Infrastructure.Audit;

/// <summary>
/// 审计日志中间件。
/// 审计日志的业务字段（Action / ResourceType / ResourceId / OperatorId）由应用服务
/// （<see cref="Leno.UserAuth.Application.Services.UserAdminAppService"/>、
/// <see cref="Leno.UserAuth.Application.Services.PermissionAppService"/>、
/// <see cref="Leno.UserAuth.Application.Services.OAuthClientAppService"/>）
/// 在事务内显式创建 <see cref="Leno.UserAuth.Domain.Aggregates.AuditLog"/> 实体写入；
/// 技术上下文（IP / UserAgent / TraceId）由 <see cref="AuditLogInterceptor"/> 在 SaveChanges 时注入。
/// 本中间件不再在请求阶段解析并存储审计上下文到 HttpContext.Items（原值从未被消费，属于死代码，已移除）。
/// 保留中间件注册位以便未来扩展（如在响应阶段记录 HTTP 状态码审计）。
/// </summary>
public sealed class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;

    public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}
