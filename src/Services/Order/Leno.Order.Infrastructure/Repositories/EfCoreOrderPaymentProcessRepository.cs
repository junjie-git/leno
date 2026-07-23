using Leno.Order.Application.ProcessManagers;
using Leno.Order.Application.ProcessManagers.States;
using Microsoft.EntityFrameworkCore;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// <see cref="IOrderPaymentProcessRepository"/> 的 EF Core 实现，复用 <see cref="OrderDbContext"/>。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SaveAsync"/> 通过 EF Core 变更跟踪与附加机制判断新增或更新：
/// <list type="bullet">
/// <item>已跟踪（同 DbContext 通过 <see cref="GetByOrderIdAsync"/> 加载并修改）：变更自动检测，无需额外操作。</item>
/// <item>新实体（<see cref="OrderPaymentProcessState.RowVersion"/> 为空）：调用 <see cref="DbSet{TEntity}.AddAsync"/> 标记为 Added。</item>
/// <item>分离的既有实体（<see cref="OrderPaymentProcessState.RowVersion"/> 非空）：Attach 后标记 Modified，
///   EF Core 使用实体携带的 RowVersion 作为 OriginalValue 写入 UPDATE WHERE 子句，实现乐观锁。
///   若并发更新导致 DB 中 row_version 已变，WHERE 不匹配，抛 <see cref="DbUpdateConcurrencyException"/>。</item>
/// </list>
/// </para>
/// <para>
/// 查询方法 <see cref="GetByOrderIdAsync"/> 与 <see cref="GetByProcessIdAsync"/> 利用
/// <see cref="Configurations.OrderPaymentProcessStateConfiguration"/> 配置的唯一索引 / 主键索引高效定位，
/// 并将实体加入 <see cref="DbContext.ChangeTracker"/> 跟踪，后续属性修改由变更跟踪自动捕获。
/// </para>
/// </remarks>
public sealed class EfCoreOrderPaymentProcessRepository : IOrderPaymentProcessRepository
{
    private readonly OrderDbContext _context;

    public EfCoreOrderPaymentProcessRepository(OrderDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task SaveAsync(OrderPaymentProcessState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        // 1. 检查是否已被当前 DbContext 跟踪（通过 GetByOrderIdAsync/GetByProcessIdAsync 加载的场景）
        //    跟踪实体的属性变更由 ChangeTracker 自动捕获，无需显式操作
        var tracked = _context.OrderPaymentProcesses.Local.FirstOrDefault(s => s.ProcessId == state.ProcessId);
        if (tracked is not null)
        {
            if (!ReferenceEquals(tracked, state))
            {
                // 不同实例（如反序列化或外部构造）：将参数值复制到已跟踪实例
                // 注意：RowVersion 作为 rowversion 由 EF Core 特殊处理，不会写入 SET 子句
                _context.Entry(tracked).CurrentValues.SetValues(state);
            }
            return;
        }

        // 2. 未跟踪：根据 RowVersion 判断新增或既有
        //    新实体 RowVersion 为空数组（OrderPaymentProcessState 初始默认值）
        //    既有实体 RowVersion 由 DB 加载，为非空 byte[]
        var isNew = state.RowVersion is null || state.RowVersion.Length == 0;
        if (isNew)
        {
            await _context.OrderPaymentProcesses.AddAsync(state, ct);
        }
        else
        {
            // 分离的既有实体：Attach 并标记 Modified
            // EF Core 将实体携带的 RowVersion 作为 OriginalValue，写入 UPDATE WHERE 子句实现乐观锁
            _context.OrderPaymentProcesses.Attach(state);
            _context.Entry(state).State = EntityState.Modified;
        }
    }

    /// <inheritdoc />
    public Task<OrderPaymentProcessState?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => _context.OrderPaymentProcesses
            .FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

    /// <inheritdoc />
    public Task<OrderPaymentProcessState?> GetByProcessIdAsync(Guid processId, CancellationToken ct = default)
        => _context.OrderPaymentProcesses
            .FirstOrDefaultAsync(s => s.ProcessId == processId, ct);
}
