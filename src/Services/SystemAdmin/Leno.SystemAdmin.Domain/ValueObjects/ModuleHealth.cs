using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 模块健康状态值对象，表示单个模块的健康检查结果。
/// 不可变记录，包含模块名称、状态、依赖项列表与检查时间。
/// </summary>
public sealed record ModuleHealth
{
    private const int MaxModuleNameLength = 128;

    /// <summary>模块名称，如 "Order", "Payment"。</summary>
    public string Module { get; }

    /// <summary>模块健康状态。</summary>
    public ModuleHealthStatus Status { get; }

    /// <summary>依赖模块名称列表。</summary>
    public List<string> Dependencies { get; }

    /// <summary>检查时间。</summary>
    public DateTime CheckedAt { get; }

    /// <summary>检查耗时（毫秒），-1 表示超时。</summary>
    public long ResponseTimeMs { get; }

    /// <summary>错误信息，健康时为空。</summary>
    public string? ErrorMessage { get; }

    public ModuleHealth(
        string module,
        ModuleHealthStatus status,
        List<string> dependencies,
        DateTime checkedAt,
        long responseTimeMs = 0,
        string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new SystemAdminDomainException("模块名称不可为空", "MODULE_NAME_EMPTY");
        }

        if (module.Trim().Length > MaxModuleNameLength)
        {
            throw new SystemAdminDomainException($"模块名称长度不可超过 {MaxModuleNameLength} 字符", "MODULE_NAME_LENGTH");
        }

        if (!Enum.IsDefined(status))
        {
            throw new SystemAdminDomainException("模块健康状态取值非法", "MODULE_STATUS_INVALID");
        }

        Module = module.Trim();
        Status = status;
        Dependencies = dependencies ?? [];
        CheckedAt = checkedAt;
        ResponseTimeMs = responseTimeMs;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 创建健康模块的结果。
    /// </summary>
    public static ModuleHealth Healthy(string module, List<string>? dependencies = null, long responseTimeMs = 0)
        => new(module, ModuleHealthStatus.Healthy, dependencies ?? [], DateTime.UtcNow, responseTimeMs);

    /// <summary>
    /// 创建降级模块的结果。
    /// </summary>
    public static ModuleHealth Degraded(string module, string errorMessage, List<string>? dependencies = null, long responseTimeMs = 0)
        => new(module, ModuleHealthStatus.Degraded, dependencies ?? [], DateTime.UtcNow, responseTimeMs, errorMessage);

    /// <summary>
    /// 创建不健康模块的结果（含超时）。
    /// </summary>
    public static ModuleHealth Unhealthy(string module, string errorMessage, List<string>? dependencies = null, long responseTimeMs = -1)
        => new(module, ModuleHealthStatus.Unhealthy, dependencies ?? [], DateTime.UtcNow, responseTimeMs, errorMessage);
}