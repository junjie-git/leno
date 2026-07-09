namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 泛型仓储基接口，定义在领域层，由基础设施层实现。
/// 只暴露聚合根级别的操作，不暴露内部实体。
/// </summary>
/// <typeparam name="T">聚合根类型。</typeparam>
public interface IRepository<T> where T : AggregateRoot
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(T aggregate, CancellationToken ct = default);

    Task UpdateAsync(T aggregate, CancellationToken ct = default);

    Task RemoveAsync(T aggregate, CancellationToken ct = default);
}
