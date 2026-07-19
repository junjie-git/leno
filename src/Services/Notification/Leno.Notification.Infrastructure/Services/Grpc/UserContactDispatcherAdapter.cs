using Leno.Infrastructure.AntiCorruption;
using Leno.Notification.Domain.Services;

namespace Leno.Notification.Infrastructure.Services.Grpc;

/// <summary>
/// 用户联系方式防腐层双轨调度适配器（M4 双轨方案）。
/// 实现 <see cref="IUserContactService"/>，内部委托 <see cref="AntiCorruptionDispatcher{IUserContactService}"/>
/// 在 HttpClient 与 gRPC 实现间按 <c>UseGrpc</c> 开关与熔断状态选择。
/// 注：<see cref="AntiCorruptionDispatcher{TService}"/> 本身不实现 <c>TService</c>，
/// 故需本适配器作为 DI 容器中 <see cref="IUserContactService"/> 的具体实现。
/// </summary>
public sealed class UserContactDispatcherAdapter : IUserContactService
{
    private readonly AntiCorruptionDispatcher<IUserContactService> _dispatcher;

    public UserContactDispatcherAdapter(
        AntiCorruptionDispatcher<IUserContactService> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public Task<UserContactInfo?> GetContactsAsync(Guid userId, CancellationToken ct = default)
        => _dispatcher.ExecuteAsync(s => s.GetContactsAsync(userId, ct), ct);
}
