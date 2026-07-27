using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Leno.ApiGateway.Transforms;

/// <summary>
/// YARP 自定义 Transform Provider，对所有路由注册：
/// <list type="bullet">
/// <item>RequestTransform：从已验签的 JWT Claims 提取用户上下文注入下游请求头
/// （X-User-Id / X-Role / X-Shop-Id / X-Internal-Call）</item>
/// <item>ResponseTransform：从响应中移除 X-Internal-Call 防止内部 Header 泄露给客户端</item>
/// </list>
/// 依赖阶段二 JwtAuthMiddleware 已将 Claims 填充到 HttpContext.User。
/// </summary>
public sealed class UserContextTransformProvider : ITransformProvider
{
    public const string XUserId = "X-User-Id";
    public const string XRole = "X-Role";
    public const string XShopId = "X-Shop-Id";
    public const string XInternalCall = "X-Internal-Call";

    /// <summary>
    /// 域拆分迁移阶段2：灰度决策请求头。由 <see cref="Middleware.GrayscaleRoutingMiddleware"/> 设置，
    /// 仅用于 YARP 路由匹配（Header matcher），必须在转发到后端前移除，防止内部决策头泄露。
    /// </summary>
    private const string XGrayscaleDecision = "X-Grayscale-Decision";

    /// <summary>
    /// 测试角色头：灰度中间件在测试场景下用作 userId hash 输入。
    /// 必须在转发到后端前移除，防止测试头泄露到生产后端。
    /// </summary>
    private const string XTestRole = "X-Test-Role";

    /// <summary>
    /// Claim 类型与 Spec 4.1 JWT Claims 对齐：Sub=UserId, Role=角色, shop_id=店铺ID。
    /// </summary>
    private const string ClaimSub = "Sub";
    private const string ClaimRole = "Role";
    private const string ClaimShopId = "shop_id";

    public void Apply(TransformBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.AddRequestTransform(rc =>
        {
            ApplyUserContextHeaders(rc.HttpContext, rc.ProxyRequest);
            // 域拆分迁移阶段2：移除灰度决策头和测试头，防止内部头泄露到后端服务
            rc.ProxyRequest.Headers.Remove(XGrayscaleDecision);
            rc.ProxyRequest.Headers.Remove(XTestRole);
            return ValueTask.CompletedTask;
        });

        context.AddResponseTransform(rc =>
        {
            RemoveInternalHeaders(rc.HttpContext);
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// 从 HttpContext.User.Claims 提取用户上下文，注入到下游代理请求 Header。
    /// 仅当 Claim 存在且非空时注入；X-Internal-Call 固定注入 "true" 标记请求来源为网关。
    /// </summary>
    internal static void ApplyUserContextHeaders(HttpContext httpContext, HttpRequestMessage proxyRequest)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(proxyRequest);

        var user = httpContext.User;

        var userId = user.FindFirst(ClaimSub)?.Value;
        var role = user.FindFirst(ClaimRole)?.Value;
        var shopId = user.FindFirst(ClaimShopId)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            proxyRequest.Headers.TryAddWithoutValidation(XUserId, userId);
        }
        if (!string.IsNullOrEmpty(role))
        {
            proxyRequest.Headers.TryAddWithoutValidation(XRole, role);
        }
        if (!string.IsNullOrEmpty(shopId))
        {
            proxyRequest.Headers.TryAddWithoutValidation(XShopId, shopId);
        }

        proxyRequest.Headers.TryAddWithoutValidation(XInternalCall, "true");
    }

    /// <summary>
    /// 从响应中移除 X-Internal-Call Header，防止内部标记泄露到客户端。
    /// </summary>
    internal static void RemoveInternalHeaders(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.Headers.Remove(XInternalCall);
    }

    public void ValidateRoute(TransformRouteValidationContext context)
    {
        // 无路由级校验
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
        // 无 Cluster 级校验
    }
}
