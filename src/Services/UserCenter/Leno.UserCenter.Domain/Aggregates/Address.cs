using Leno.SharedKernel.Abstractions;

namespace Leno.UserCenter.Domain.Aggregates;

/// <summary>
/// 收货地址聚合根骨架（Task A5 占位，Task A6 从 UserAuth.Domain 迁入完整实现）。
/// </summary>
public sealed class Address : AggregateRoot
{
    private Address() { }

    private Address(Guid id) : base(id) { }
}
