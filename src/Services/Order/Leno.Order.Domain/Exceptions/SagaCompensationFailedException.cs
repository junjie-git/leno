namespace Leno.Order.Domain.Exceptions;

/// <summary>
/// Saga 补偿失败异常，表示至少一个补偿动作（释放库存/积分/优惠券）失败。
/// 触发该异常时应记录告警并人工介入，避免资源永久泄漏。
/// </summary>
public sealed class SagaCompensationFailedException : Exception
{
    /// <summary>补偿失败的分组信息列表。</summary>
    public IReadOnlyList<CompensationFailure> Failures { get; }

    public SagaCompensationFailedException(IReadOnlyList<CompensationFailure> failures)
        : base($"Saga 补偿失败，{failures.Count} 个补偿动作失败：{string.Join("; ", failures.Select(f => $"{f.ActionType} OrderId={f.OrderId}: {f.ErrorMessage}"))}")
    {
        Failures = failures;
    }
}

/// <summary>
/// 补偿动作失败记录。
/// </summary>
public sealed record CompensationFailure(Guid OrderId, string ActionType, string ErrorMessage);
