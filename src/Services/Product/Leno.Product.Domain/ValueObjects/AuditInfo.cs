namespace Leno.Product.Domain.ValueObjects;

/// <summary>
/// 商品审核记录值对象，记录每次审核通过/驳回的操作人、结果、原因与时间。
/// 不可变，通过工厂方法创建。
/// </summary>
public sealed record AuditInfo
{
    /// <summary>操作人标识。</summary>
    public string OperatorId { get; private set; } = string.Empty;

    /// <summary>操作人名称。</summary>
    public string OperatorName { get; private set; } = string.Empty;

    /// <summary>审核结果：Approved / Rejected。</summary>
    public string Result { get; private set; } = string.Empty;

    /// <summary>驳回原因，审核通过时可为空。</summary>
    public string? Reason { get; private set; }

    /// <summary>审核时间（UTC）。</summary>
    public DateTime AuditedAt { get; private set; }

    private AuditInfo() { }

    private AuditInfo(string operatorId, string operatorName, string result, string? reason, DateTime auditedAt)
    {
        OperatorId = operatorId;
        OperatorName = operatorName;
        Result = result;
        Reason = reason;
        AuditedAt = auditedAt;
    }

    /// <summary>
    /// 创建审核通过记录。
    /// </summary>
    /// <param name="operatorId">操作人标识。</param>
    /// <param name="operatorName">操作人名称。</param>
    public static AuditInfo Approved(string operatorId, string operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException("操作人标识不可为空", nameof(operatorId));
        }

        if (string.IsNullOrWhiteSpace(operatorName))
        {
            throw new ArgumentException("操作人名称不可为空", nameof(operatorName));
        }

        return new AuditInfo(operatorId.Trim(), operatorName.Trim(), "Approved", null, DateTime.UtcNow);
    }

    /// <summary>
    /// 创建审核驳回记录。
    /// </summary>
    /// <param name="operatorId">操作人标识。</param>
    /// <param name="operatorName">操作人名称。</param>
    /// <param name="reason">驳回原因，不可为空。</param>
    public static AuditInfo Rejected(string operatorId, string operatorName, string reason)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException("操作人标识不可为空", nameof(operatorId));
        }

        if (string.IsNullOrWhiteSpace(operatorName))
        {
            throw new ArgumentException("操作人名称不可为空", nameof(operatorName));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("驳回原因不可为空", nameof(reason));
        }

        return new AuditInfo(operatorId.Trim(), operatorName.Trim(), "Rejected", reason.Trim(), DateTime.UtcNow);
    }
}