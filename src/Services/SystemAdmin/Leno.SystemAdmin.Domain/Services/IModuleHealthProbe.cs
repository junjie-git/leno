using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 模块健康探测接口，定义在领域层，由基础设施层实现。
/// 负责对指定模块端点执行健康检查并返回结果。
/// </summary>
public interface IModuleHealthProbe
{
    /// <summary>
    /// 对指定模块端点执行健康探测。
    /// </summary>
    /// <param name="moduleEndpoint">模块健康检查端点 URL，如 "http://order-service/health"。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>模块健康检查结果。</returns>
    Task<ModuleHealth> ProbeAsync(string moduleEndpoint, CancellationToken ct = default);
}